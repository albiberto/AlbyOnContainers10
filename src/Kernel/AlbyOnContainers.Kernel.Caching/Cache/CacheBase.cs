using AlbyOnContainers.Kernel.Caching.Abstractions;
using AlbyOnContainers.Kernel.Caching.Keys;
using Microsoft.Extensions.DependencyInjection;
using ZiggyCreatures.Caching.Fusion;

namespace AlbyOnContainers.Kernel.Caching.Cache;

public abstract class CacheBase<TDto>(IFusionCache cache, IServiceScopeFactory scopeFactory)
{
    protected virtual ICacheKey GlobalCacheKey => new DefaultCacheKey<TDto>();

    protected abstract Task<List<TDto>> FetchAllFromDbAsync(IServiceProvider scopeProvider, CancellationToken ct);

    public async Task<List<TDto>> GetOrSetAllAsync(CancellationToken cancellationToken = default) =>
        await cache.GetOrSetAsync<List<TDto>>(
            GlobalCacheKey.Value,
            async (_, ct) =>
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                return await FetchAllFromDbAsync(scope.ServiceProvider, ct);
            },
            token: cancellationToken);

    public async Task ExpireAllAsync(CancellationToken ct = default) => await cache.ExpireAsync(GlobalCacheKey.Value, token: ct);

    public async Task RemoveAllAsync(CancellationToken ct = default) => await cache.RemoveAsync(GlobalCacheKey.Value, token: ct);
}