using System.Diagnostics.Metrics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ProductInformationManager.Infrastructure;
using ProductInformationManager.Messages;
using ZiggyCreatures.Caching.Fusion;
using AlbyOnContainers.Shared.Application.Cache;

namespace ProductInformationManager.Application.Cache;

public sealed class CategoryCache : CacheBase<CategoryDto>
{
    private readonly ILogger<CategoryCache> _logger;
    private readonly Counter<long> _cacheReloads;
    private readonly Histogram<int> _cacheEntryCount;

    public CategoryCache(IFusionCache cache, IServiceProvider provider, ILogger<CategoryCache> logger, IMeterFactory meterFactory) : base(cache, provider)
    {
        _logger = logger;
        
        var meter = meterFactory.Create("ProductInformationManager.Application");
        
        _cacheReloads = meter.CreateCounter<long>("pim_category_cache_reloads", description: "Number of times the category cache is reloaded from the database.");
        _cacheEntryCount = meter.CreateHistogram<int>("pim_category_cache_entries", unit: "{entry}", description: "Number of category entries loaded into cache.");
    }

    protected override async Task<List<CategoryDto>> FetchDataAsync(IServiceProvider scopedProvider, CancellationToken ct)
    {
        var db = scopedProvider.GetRequiredService<ProductContext>();

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

        _cacheReloads.Add(1);
        _cacheEntryCount.Record(categories.Count);

        _logger.LogInformation("Cache reloaded {CacheName} with {CategoryCount} categories", nameof(CategoryCache), categories.Count);

        return categories;
    }
}