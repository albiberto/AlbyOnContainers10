using ZiggyCreatures.Caching.Fusion;

namespace AlbyOnContainers.Kernel.Caching.Cache;

public sealed class AlbyCache(IFusionCache cache) : IAlbyCache
{
    public async Task<T?> GetOrSetAsync<T>(string key, Func<CancellationToken, Task<T>> factory, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(factory);

        return await cache.GetOrSetAsync<T?>(
            key,
            async (_, factoryCt) => await factory(factoryCt),
            token: ct);
    }

    public Task RemoveAsync(string key, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return cache.RemoveAsync(key, token: ct).AsTask();
    }

    public Task SetAsync<T>(string key, T value, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return cache.SetAsync(key, value, token: ct).AsTask();
    }
}
