namespace AlbyOnContainers.Kernel.Caching.IntegrationTests;

using Caching.Abstractions;
using Caching.Cache;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

[TestFixture]
public sealed class CacheRoundTripTests : IntegrationTestBase
{
    private IHost _host = null!;
    private ICache _cache = null!;

    [SetUp]
    public async Task SetUp()
    {
        _host = await CreateHostAsync();
        _cache = _host.Services.GetRequiredService<ICache>();
    }

    [TearDown]
    public async Task TearDown()
    {
        await _host.StopAsync();
        _host.Dispose();
    }

    [Test]
    public async Task Set_then_GetOrSet_returns_cached_value_without_invoking_factory()
    {
        var key = Key.Custom($"roundtrip:{Guid.NewGuid():N}");

        await _cache.SetAsync(key, "stored");

        var factoryCalled = false;
        var observed = await _cache.GetOrSetAsync(key, _ =>
        {
            factoryCalled = true;
            return Task.FromResult("from-factory");
        });

        Assert.Multiple(() =>
        {
            Assert.That(observed, Is.EqualTo("stored"));
            Assert.That(factoryCalled, Is.False, "Factory must not run when value is cached.");
        });
    }

    [Test]
    public async Task Remove_evicts_value_so_next_GetOrSet_invokes_factory()
    {
        var key = Key.Custom($"removable:{Guid.NewGuid():N}");

        await _cache.SetAsync(key, "v1");
        await _cache.RemoveAsync(key);

        var observed = await _cache.GetOrSetAsync(key, _ => Task.FromResult("v2"));

        Assert.That(observed, Is.EqualTo("v2"));
    }

    [Test]
    public async Task GetOrSetAsync_persists_complex_object_via_MessagePack_through_Redis()
    {
        var key = Key.Custom($"complex:{Guid.NewGuid():N}");
        var payload = new Sample(42, "alice", new[] { "x", "y", "z" });

        await _cache.SetAsync(key, payload);

        // Read from a brand-new host so the value MUST come from Redis (L2),
        // proving the MessagePack serializer round-trips a non-trivial graph.
        using var freshHost = await CreateHostAsync();
        var freshCache = freshHost.Services.GetRequiredService<ICache>();

        var roundTripped = await freshCache.GetOrSetAsync<Sample>(key, _ => throw new InvalidOperationException("L2 should hit"));

        Assert.Multiple(() =>
        {
            Assert.That(roundTripped, Is.Not.Null);
            Assert.That(roundTripped!.Id, Is.EqualTo(42));
            Assert.That(roundTripped.Name, Is.EqualTo("alice"));
            Assert.That(roundTripped.Tags, Is.EqualTo(new[] { "x", "y", "z" }));
        });

        await freshHost.StopAsync();
    }

    public sealed record Sample(int Id, string Name, string[] Tags);
}
