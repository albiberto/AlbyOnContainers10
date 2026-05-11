namespace AlbyOnContainers.Kernel.Caching.IntegrationTests;

using AlbyOnContainers.Kernel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ZiggyCreatures.Caching.Fusion;

[TestFixture]
public sealed class FusionCacheRoundTripTests
{
    [Test]
    public async Task FusionCache_L1_RoundTripsComplexObject()
    {
        // Arrange
        var hostBuilder = Host.CreateApplicationBuilder();
        var kernelBuilder = hostBuilder.AddKernel();
        kernelBuilder.WithCaching();

        using var host = hostBuilder.Build();
        await host.StartAsync();

        var cache = host.Services.GetRequiredService<IFusionCache>();
        var key = $"complex:{Guid.NewGuid():N}";
        var payload = new Sample(42, "alice", ["x", "y", "z"]);

        // Act
        await cache.SetAsync(key, payload);
        var roundTripped = await cache.GetOrSetAsync<Sample>(
            key,
            _ => throw new InvalidOperationException("L1 should hit"),
            token: CancellationToken.None);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(roundTripped.Id, Is.EqualTo(42));
            Assert.That(roundTripped.Name, Is.EqualTo("alice"));
            Assert.That(roundTripped.Tags, Is.EqualTo(new[] { "x", "y", "z" }));
        });

        await host.StopAsync();
    }

    public sealed record Sample(int Id, string Name, string[] Tags);
}
