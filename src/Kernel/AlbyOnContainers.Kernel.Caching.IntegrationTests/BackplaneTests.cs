namespace AlbyOnContainers.Kernel.Caching.IntegrationTests;

using Caching.Abstractions;
using Caching.Cache;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

[TestFixture]
public sealed class BackplaneTests : IntegrationTestBase
{
    private IHost _hostA = null!;
    private IHost _hostB = null!;
    private ICache _cacheA = null!;
    private ICache _cacheB = null!;

    [SetUp]
    public async Task SetUp()
    {
        // Two independent hosts attached to the same Redis instance simulate two
        // microservice replicas: each owns its own L1 (memory) and shares the L2
        // (Redis) plus the FusionCache backplane channel.
        _hostA = await CreateHostAsync();
        _hostB = await CreateHostAsync();
        _cacheA = _hostA.Services.GetRequiredService<ICache>();
        _cacheB = _hostB.Services.GetRequiredService<ICache>();
    }

    [TearDown]
    public async Task TearDown()
    {
        await _hostA.StopAsync();
        await _hostB.StopAsync();
        _hostA.Dispose();
        _hostB.Dispose();
    }

    [Test]
    public async Task RemoveAsync_on_one_host_invalidates_L1_on_the_other_host_via_backplane()
    {
        var key = Key.Custom($"backplane:{Guid.NewGuid():N}");

        // Both hosts populate their L1 via the same Redis L2.
        await _cacheA.SetAsync(key, "v1");
        var observedOnB = await _cacheB.GetOrSetAsync(key, _ => Task.FromResult("from-factory"));
        Assert.That(observedOnB, Is.EqualTo("v1"), "Host B should read v1 from Redis L2.");

        // Host A removes the key. The backplane must notify Host B so its L1
        // entry is invalidated and the next read goes back through the factory.
        await _cacheA.RemoveAsync(key);

        var deadline = DateTime.UtcNow.AddSeconds(5);
        string? observed = null;
        while (DateTime.UtcNow < deadline)
        {
            observed = await _cacheB.GetOrSetAsync(key, _ => Task.FromResult("v2"));
            if (observed == "v2") break;
            await Task.Delay(100);
        }

        Assert.That(observed, Is.EqualTo("v2"), "Backplane should have invalidated Host B's L1 within 5s.");
    }
}
