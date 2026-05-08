namespace AlbyOnContainers.Kernel.Caching.UnitTests;

using Caching.Abstractions;
using Caching.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NSubstitute;
using StackExchange.Redis;

[TestFixture]
public sealed class CachingExtensionsTests
{
    private static HostApplicationBuilder NewBuilder() => Host.CreateApplicationBuilder();

    [Test]
    public async Task WithCaching_Lambda_RegistersOptionsAndICache()
    {
        var builder = NewBuilder();
        builder.Services.AddSingleton(Substitute.For<IConnectionMultiplexer>());

        builder.AddKernel().WithCaching(opt =>
        {
            opt.Duration = TimeSpan.FromMinutes(5);
            opt.IsFailSafeEnabled = false;
        });

        using var host = builder.Build();
        await host.StartAsync();

        var cache = host.Services.GetService<ICache>();
        var optionsMonitor = host.Services.GetRequiredService<IOptionsMonitor<CachingOptions>>();

        Assert.Multiple(() =>
        {
            Assert.That(cache, Is.Not.Null);
            Assert.That(optionsMonitor.Get(Microsoft.Extensions.Options.Options.DefaultName).Duration, Is.EqualTo(TimeSpan.FromMinutes(5)));
            Assert.That(optionsMonitor.Get(Microsoft.Extensions.Options.Options.DefaultName).IsFailSafeEnabled, Is.False);
        });

        await host.StopAsync();
    }

    [Test]
    public async Task WithKeyedCaching_RegistersKeyedICache()
    {
        const string key = "secondary";
        var builder = NewBuilder();
        builder.Services.AddKeyedSingleton(key, (_, _) => Substitute.For<IConnectionMultiplexer>());

        builder.AddKernel().WithKeyedCaching(key, opt => opt.Duration = TimeSpan.FromMinutes(1));

        using var host = builder.Build();
        await host.StartAsync();

        var cache = host.Services.GetKeyedService<ICache>(key);

        Assert.That(cache, Is.Not.Null);

        await host.StopAsync();
    }

    [Test]
    public void WithCaching_WithoutMultiplexer_FailsFastAtStartup()
    {
        var builder = NewBuilder();
        builder.AddKernel().WithCaching();

        using var host = builder.Build();

        var ex = Assert.ThrowsAsync<InvalidOperationException>(() => host.StartAsync());

        Assert.That(ex!.Message, Does.Contain("IConnectionMultiplexer"));
    }

    [Test]
    public void WithKeyedCaching_WithoutKeyedMultiplexer_FailsFastAtStartup()
    {
        const string key = "missing";
        var builder = NewBuilder();
        builder.AddKernel().WithKeyedCaching(key);

        using var host = builder.Build();

        var ex = Assert.ThrowsAsync<InvalidOperationException>(() => host.StartAsync());

        Assert.That(ex!.Message, Does.Contain(key));
    }
}
