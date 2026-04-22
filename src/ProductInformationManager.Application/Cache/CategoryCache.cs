using System.Diagnostics.Metrics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProductInformationManager.Infrastructure;
using ProductInformationManager.Messages;
using ZiggyCreatures.Caching.Fusion;

namespace ProductInformationManager.Application.Cache;

public sealed class CategoryCache(
    IFusionCache cache,
    IServiceProvider provider,
    ILogger<CategoryCache> logger) : CacheBase<CategoryDto>(cache, provider)
{
    private static readonly Meter Meter = new("ProductInformationManager.Application");
    private static readonly Counter<long> CacheReloads = Meter.CreateCounter<long>(
        "pim_category_cache_reloads",
        description: "Number of times the category cache is reloaded from the database.");
    private static readonly Histogram<int> CacheEntryCount = Meter.CreateHistogram<int>(
        "pim_category_cache_entries",
        unit: "{entry}",
        description: "Number of category entries loaded into cache.");

    protected override async Task<List<CategoryDto>> FetchDataFromDbAsync(ProductContext db, CancellationToken ct)
    {
        var categories = await db.Categories
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

        CacheReloads.Add(1);
        CacheEntryCount.Record(categories.Count);

        logger.LogInformation(
            "PIM cache reloaded {CacheName} with {CategoryCount} categories",
            nameof(CategoryCache),
            categories.Count);

        return categories;
    }
}
