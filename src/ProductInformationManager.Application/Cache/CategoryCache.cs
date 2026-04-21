using Microsoft.EntityFrameworkCore;
using ProductInformationManager.Infrastructure;
using ProductInformationManager.Messages;
using ZiggyCreatures.Caching.Fusion;

namespace ProductInformationManager.Application.Cache;

public sealed class CategoryCache(IFusionCache cache, IServiceProvider provider) : CacheBase<CategoryDto>(cache, provider)
{
    protected override async Task<List<CategoryDto>> FetchDataFromDbAsync(ProductContext db, CancellationToken ct) =>
        await db.Categories
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