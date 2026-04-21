using AlbyOnContainers.Shared.Domain;
using MassTransit;
using ProductInformationManager.Application.Cache;
using ProductInformationManager.Messages;

namespace ProductInformationManager.Application;

public class GetRootCategoriesConsumer(CategoryCache cache) : IConsumer<GetRootCategories>
{
    public async Task Consume(ConsumeContext<GetRootCategories> context)
    {
        var all = await cache.GetAllAsync(context.CancellationToken);
        
        var roots = all
            .Where(c => c.ParentId == null)
            .OrderBy(c => c.Name)
            .ToList();
        
        await context.RespondAsync(new GetCategoriesResult(roots)); 
    }
}

public class GetChildCategoriesConsumer(CategoryCache cache) : IConsumer<GetChildCategories>
{
    public async Task Consume(ConsumeContext<GetChildCategories> context)
    {
        var all = await cache.GetAllAsync(context.CancellationToken);
        
        var children = all
            .Where(c => c.ParentId == context.Message.ParentId)
            .OrderBy(c => c.Name)
            .ToList();

        await context.RespondAsync(new GetCategoriesResult(children)); 
    }
}

public class GetCategoryByIdConsumer(CategoryCache cache) : IConsumer<GetCategoryById>
{
    public async Task Consume(ConsumeContext<GetCategoryById> context)
    {
        var all = await cache.GetAllAsync(context.CancellationToken);
        
        var category = all.FirstOrDefault(c => c.Id == context.Message.Id);
        
        if (category is null) throw new DomainException($"Category with ID {context.Message.Id} not found.");

        await context.RespondAsync(new GetCategoryResult(category));
    }
}

public class SearchCategoriesConsumer(CategoryCache cache) : IConsumer<SearchCategories>
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

        var all = await cache.GetAllAsync(context.CancellationToken);

        var results = all
            .Where(c => c.Name.Contains(lowerPattern, StringComparison.InvariantCultureIgnoreCase))
            .OrderBy(c => c.Path)
            .Select(c => new CategoryFlatDto(c.Id, c.Name, c.Path, c.Path.Split('.').Length - 1))
            .ToList();

        await context.RespondAsync(new SearchCategoriesResult(results));
    }
}