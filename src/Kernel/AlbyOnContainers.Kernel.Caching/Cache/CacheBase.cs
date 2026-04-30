using Microsoft.Extensions.DependencyInjection;
using ZiggyCreatures.Caching.Fusion;
using AlbyOnContainers.Kernel.Caching.Keys;

namespace AlbyOnContainers.Kernel.Caching.Cache;

public abstract class CacheBase<TDto>(IFusionCache cache, IServiceScopeFactory scopeFactory)
{
    protected virtual FusionCacheEntryOptions? EntryOptions => null;

    // ==============================================================================
    // ALL ENTITIES
    // ==============================================================================
    
    public async Task<List<TDto>> GetOrSetAllAsync(CancellationToken cancellationToken = default)
    {
        var key = new DefaultCacheKey<TDto>().Value;

        return await cache.GetOrSetAsync<List<TDto>>(
            key,
            async (_, ct) =>
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                return await FetchAllFromDbAsync(scope.ServiceProvider, ct);
            },
            options: EntryOptions,
            token: cancellationToken);
    }

    protected abstract Task<List<TDto>> FetchAllFromDbAsync(IServiceProvider sp, CancellationToken ct);

    // ==============================================================================
    // SINGLE ENTITY
    // ==============================================================================

    public async Task<TDto?> GetOrSetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var key = new DefaultCacheKey<TDto>(id).Value;

        return await cache.GetOrSetAsync<TDto?>(
            key,
            async (_, ct) =>
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                return await FetchSingleFromDbAsync(id, scope.ServiceProvider, ct);
            },
            options: EntryOptions,
            token: cancellationToken);
    }

    protected abstract Task<TDto?> FetchSingleFromDbAsync(Guid id, IServiceProvider sp, CancellationToken ct);

    // ==============================================================================
    // INVALIDATION
    // ==============================================================================

    public Task InvalidateAllAsync(CancellationToken cancellationToken = default)
    {
        var key = new DefaultCacheKey<TDto>().Value;
        return cache.RemoveAsync(key, token: cancellationToken).AsTask();
    }

    public Task InvalidateByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var key = new DefaultCacheKey<TDto>(id).Value;
        return cache.RemoveAsync(key, token: cancellationToken).AsTask();
    }
}