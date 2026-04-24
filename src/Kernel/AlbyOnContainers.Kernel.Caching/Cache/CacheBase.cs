using Microsoft.Extensions.DependencyInjection;
using ZiggyCreatures.Caching.Fusion;

namespace AlbyOnContainers.Kernel.Caching.Cache;

public abstract class CacheBase<TDto>(IFusionCache cache, IServiceProvider provider)
{
    protected virtual string CacheKey => $"{typeof(TDto).Name.ToLowerInvariant()}:all";
    
    protected abstract Task<List<TDto>> FetchDataAsync(IServiceProvider scopedProvider, CancellationToken ct);
    
    public async Task<List<TDto>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await cache.GetOrSetAsync<List<TDto>>(
            CacheKey,
            async (_, token) =>
            {
                using var scope = provider.CreateScope();
                return await FetchDataAsync(scope.ServiceProvider, token);
            },
            token: cancellationToken);

    public async Task InvalidateAsync(CancellationToken ct = default) => await cache.RemoveAsync(CacheKey, token: ct);
}