namespace AlbyOnContainers.Kernel.Caching.Cache;

using Abstractions;
using ZiggyCreatures.Caching.Fusion;

public sealed class Cache(IFusionCache cache) : ICache
{
    public async Task<T?> GetOrSetAsync<T>(IKey key, Func<CancellationToken, Task<T>> factory, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(factory);

        return await cache.GetOrSetAsync<T?>(
            key.Value,
            async (_, factoryCt) => await factory(factoryCt),
            token: ct);
    }

    public Task RemoveAsync(IKey key, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(key);

        return cache.RemoveAsync(key.Value, token: ct).AsTask();
    }

    public Task SetAsync<T>(IKey key, T value, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(key);

        return cache.SetAsync(key.Value, value, token: ct).AsTask();
    }
}
