namespace AlbyOnContainers.Kernel.Caching.UnitTests;

using Cache;
using Caching.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using ZiggyCreatures.Caching.Fusion;

[TestFixture]
public sealed class CacheTests
{
    private ServiceProvider _provider = null!;
    private ICache _cache = null!;

    [SetUp]
    public void SetUp()
    {
        var services = new ServiceCollection();
        services.AddFusionCache();
        services.AddSingleton<ICache, Cache>();

        _provider = services.BuildServiceProvider();
        _cache = _provider.GetRequiredService<ICache>();
    }

    [TearDown]
    public void TearDown() => _provider.Dispose();

    [Test]
    public async Task GetOrSetAsync_FirstCall_InvokesFactoryAndStoresValue()
    {
        var key = Key.Custom("first-call");
        var invocations = 0;

        var first = await _cache.GetOrSetAsync(key, _ =>
        {
            Interlocked.Increment(ref invocations);
            return Task.FromResult("hello");
        });

        var second = await _cache.GetOrSetAsync(key, _ =>
        {
            Interlocked.Increment(ref invocations);
            return Task.FromResult("should-not-run");
        });

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.EqualTo("hello"));
            Assert.That(second, Is.EqualTo("hello"), "Second call must hit cache, not the factory.");
            Assert.That(invocations, Is.EqualTo(1), "Factory must run exactly once.");
        });
    }

    [Test]
    public async Task RemoveAsync_InvalidatesEntry()
    {
        var key = Key.Custom("removable");
        await _cache.SetAsync(key, "v1");

        await _cache.RemoveAsync(key);

        var afterRemove = await _cache.GetOrSetAsync(key, _ => Task.FromResult("v2"));
        Assert.That(afterRemove, Is.EqualTo("v2"));
    }

    [Test]
    public async Task SetAsync_OverridesExistingValue()
    {
        var key = Key.Custom("overridable");
        await _cache.SetAsync(key, "v1");
        await _cache.SetAsync(key, "v2");

        var current = await _cache.GetOrSetAsync(key, _ => Task.FromResult("factory-fallback"));
        Assert.That(current, Is.EqualTo("v2"));
    }

    [Test]
    public void GetOrSetAsync_NullKey_Throws() =>
        Assert.ThrowsAsync<ArgumentNullException>(() => _cache.GetOrSetAsync<string>(null!, _ => Task.FromResult("x")));

    [Test]
    public void GetOrSetAsync_NullFactory_Throws() =>
        Assert.ThrowsAsync<ArgumentNullException>(() => _cache.GetOrSetAsync<string>(Key.Custom("k"), null!));
}
