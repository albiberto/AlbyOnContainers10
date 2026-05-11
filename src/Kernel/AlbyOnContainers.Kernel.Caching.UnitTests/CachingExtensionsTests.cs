namespace AlbyOnContainers.Kernel.Caching.UnitTests;

using Caching.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ZiggyCreatures.Caching.Fusion;

[TestFixture]
public sealed class CachingExtensionsTests
{
    private static HostApplicationBuilder NewBuilder() => Host.CreateApplicationBuilder();

    [Test]
    public async Task WithCaching_FromConfiguration_RegistersFusionCacheAndOptions()
    {
        // Arrange
        var builder = NewBuilder();
        builder.Configuration["Caching:Duration"] = "00:05:00";
        builder.Configuration["Caching:IsFailSafeEnabled"] = "false";

        // Act
        builder.AddKernel().WithCaching();

        using var host = builder.Build();
        await host.StartAsync();

        var cache = host.Services.GetService<IFusionCache>();
        var optionsMonitor = host.Services.GetRequiredService<IOptionsMonitor<CachingOptions>>();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(cache, Is.Not.Null);
            Assert.That(optionsMonitor.Get(Options.DefaultName).Duration, Is.EqualTo(TimeSpan.FromMinutes(5)));
            Assert.That(optionsMonitor.Get(Options.DefaultName).IsFailSafeEnabled, Is.False);
        });

        await host.StopAsync();
    }

    [Test]
    public async Task WithCaching_RegistersUsableL1Cache()
    {
        // Arrange
        var builder = NewBuilder();
        builder.AddKernel().WithCaching();

        using var host = builder.Build();
        await host.StartAsync();

        var cache = host.Services.GetRequiredService<IFusionCache>();

        // Act
        await cache.SetAsync("l1-key", "stored");
        var observed = await cache.GetOrSetAsync<string>(
            "l1-key",
            _ => throw new InvalidOperationException("L1 should hit"),
            token: CancellationToken.None);

        // Assert
        Assert.That(observed, Is.EqualTo("stored"));

        await host.StopAsync();
    }
}
