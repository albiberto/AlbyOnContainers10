using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProductInformationManager.Infrastructure;
using ProductInformationManager.Messages;
using ZiggyCreatures.Caching.Fusion;
using AlbyOnContainers.Kernel.Caching.Cache;

namespace ProductInformationManager.Application.Cache;

public sealed class CategoryCache(IFusionCache cache, IServiceScopeFactory scopeFactory) : CacheBase<CategoryDto>(cache, scopeFactory)
{
    protected override async Task<List<CategoryDto>> FetchAllFromDbAsync(IServiceProvider scopeProvider, CancellationToken ct)
    {
        await using var db = scopeProvider.GetRequiredService<ProductContext>();

        return await db.Categories
            .AsNoTracking()
            .OrderBy(c => c.Path)
            .Select(c => new CategoryDto(
                c.Id.Value,
                c.Name,
                c.Description,
                c.Path,
                c.ParentId != null ? c.ParentId.Value : null,
                c.Children.Any()))
            .ToListAsync(ct);
    }
}