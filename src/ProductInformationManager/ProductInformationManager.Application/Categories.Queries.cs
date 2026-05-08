using AlbyOnContainers.Kernel.Caching.Cache;
using AlbyOnContainers.Kernel.Messaging.Attributes;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using ProductInformationManager.Domain;
using ProductInformationManager.Infrastructure;
using ProductInformationManager.Messages;

namespace ProductInformationManager.Application;

using AlbyOnContainers.Kernel.Caching.Abstractions;

[MediatorConsumer]
public class GetRootCategoriesConsumer(ICache cache, ProductContext db) : IConsumer<GetRootCategories>
{
    public async Task Consume(ConsumeContext<GetRootCategories> context)
    {
        var all = await cache.GetOrSetAsync(
            Key.Type<Category>("All"),
            ct => db.Categories
                .AsNoTracking()
                .OrderBy(c => c.Path)
                .Select(c => new CategoryDto(
                    c.Id.Value,
                    c.Name,
                    c.Description,
                    c.Path,
                    c.ParentId != null ? c.ParentId.Value : null,
                    c.Children.Any()))
                .ToListAsync(ct),
            context.CancellationToken) ?? [];
        
        var roots = all
            .Where(c => c.ParentId == null)
            .OrderBy(c => c.Name)
            .ToList();
        
        await context.RespondAsync(new GetCategoriesResult(roots)); 
    }
}

[MediatorConsumer]
public class GetChildCategoriesConsumer(ICache cache, ProductContext db) : IConsumer<GetChildCategories>
{
    public async Task Consume(ConsumeContext<GetChildCategories> context)
    {
        var all = await cache.GetOrSetAsync(
            Key.Type<Category>("All"),
            ct => db.Categories
                .AsNoTracking()
                .OrderBy(c => c.Path)
                .Select(c => new CategoryDto(
                    c.Id.Value,
                    c.Name,
                    c.Description,
                    c.Path,
                    c.ParentId != null ? c.ParentId.Value : null,
                    c.Children.Any()))
                .ToListAsync(ct),
            context.CancellationToken) ?? [];
        
        var children = all
            .Where(c => c.ParentId == context.Message.ParentId)
            .OrderBy(c => c.Name)
            .ToList();

        await context.RespondAsync(new GetCategoriesResult(children)); 
    }
}

[MediatorConsumer]
public class GetCategoryByIdConsumer(ICache cache, ProductContext db) : IConsumer<GetCategoryById>
{
    public async Task Consume(ConsumeContext<GetCategoryById> context)
    {
        var all = await cache.GetOrSetAsync(
            Key.Type<Category>("All"),
            ct => db.Categories
                .AsNoTracking()
                .OrderBy(c => c.Path)
                .Select(c => new CategoryDto(
                    c.Id.Value,
                    c.Name,
                    c.Description,
                    c.Path,
                    c.ParentId != null ? c.ParentId.Value : null,
                    c.Children.Any()))
                .ToListAsync(ct),
            context.CancellationToken) ?? [];
        
        var category = all.FirstOrDefault(c => c.Id == context.Message.Id);
        
        if (category is null) throw new DomainException($"Category with ID {context.Message.Id} not found.");

        await context.RespondAsync(new GetCategoryResult(category));
    }
}

[MediatorConsumer]
public class SearchCategoriesConsumer(ICache cache, ProductContext db) : IConsumer<SearchCategories>
{
    public async Task Consume(ConsumeContext<SearchCategories> context)
    {
        var pattern = context.Message.SearchPattern?.Trim();

        if (string.IsNullOrWhiteSpace(pattern) || pattern.Length <= 3)
        {
            await context.RespondAsync(new SearchCategoriesResult(new List<CategoryFlatDto>()));
            return;
        }

        var lowerPattern = pattern.ToLowerInvariant();

        var all = await cache.GetOrSetAsync(
            Key.Type<Category>("All"),
            ct => db.Categories
                .AsNoTracking()
                .OrderBy(c => c.Path)
                .Select(c => new CategoryDto(
                    c.Id.Value,
                    c.Name,
                    c.Description,
                    c.Path,
                    c.ParentId != null ? c.ParentId.Value : null,
                    c.Children.Any()))
                .ToListAsync(ct),
            context.CancellationToken) ?? [];

        var results = all
            .Where(c => c.Name.Contains(lowerPattern, StringComparison.InvariantCultureIgnoreCase))
            .OrderBy(c => c.Path)
            .Select(c => new CategoryFlatDto(c.Id, c.Name, c.Path, c.Path.Split('.').Length - 1))
            .ToList();

        await context.RespondAsync(new SearchCategoriesResult(results));
    }
}
