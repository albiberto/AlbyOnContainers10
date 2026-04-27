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

        // FIXED: Explicitly defined <List<TDto>> to help the C# compiler infer the lambda context
        return await cache.GetOrSetAsync<List<TDto>>(
            key,
            async (_, ct) =>
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                return await FetchAllFromDbAsync(scope.ServiceProvider, ct);
            },
            options: EntryOptions,
            token: cancellationToken) ?? [];
    }

    protected abstract Task<List<TDto>> FetchAllFromDbAsync(IServiceProvider sp, CancellationToken ct);

    // ==============================================================================
    // SINGLE ENTITY
    // ==============================================================================

    public async Task<TDto?> GetOrSetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var key = new DefaultCacheKey<TDto>(id).Value;

        // FIXED: Explicitly defined <TDto?> to resolve ambiguity
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

    public async Task InvalidateAllAsync(CancellationToken cancellationToken = default)
    {
        var key = new DefaultCacheKey<TDto>().Value;
        await cache.RemoveAsync(key, token: cancellationToken);
    }

    public async Task InvalidateByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var key = new DefaultCacheKey<TDto>(id).Value;
        await cache.RemoveAsync(key, token: cancellationToken);
    }
}