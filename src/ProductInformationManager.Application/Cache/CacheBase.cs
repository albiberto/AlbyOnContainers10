using Microsoft.Extensions.DependencyInjection;
using ProductInformationManager.Infrastructure;
using ZiggyCreatures.Caching.Fusion;

namespace ProductInformationManager.Application.Cache;

public abstract class CacheBase<TDto>(IFusionCache cache, IServiceProvider provider)
{
    private static readonly string CacheKey = $"pim:{typeof(TDto).Name}:all";
    
    protected abstract Task<List<TDto>> FetchDataFromDbAsync(ProductContext db, CancellationToken ct);
    
    public async Task<List<TDto>> GetAllAsync(CancellationToken ct = default) =>
        await cache.GetOrSetAsync<List<TDto>>(
            CacheKey,
            async (_, token) =>
            {
                using var scope = provider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ProductContext>();

                return await FetchDataFromDbAsync(db, token);
            },
            token: ct) ?? [];

    public async Task InvalidateAsync(CancellationToken ct = default) => await cache.RemoveAsync(CacheKey, token: ct);
}