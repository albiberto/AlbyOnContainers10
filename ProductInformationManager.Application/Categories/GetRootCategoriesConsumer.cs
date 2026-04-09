using MassTransit;

namespace ProductInformationManager.Application.Categories;

public partial class GetRootCategoriesConsumer(ProductContext db) : IConsumer<GetRootCategories>
{
    public async Task Consume(ConsumeContext<GetRootCategories> context)
    {
        var categories = await db.Categories
            .Where(c => c.ParentId == null)
            .OrderBy(c => c.Name)
            .ToListAsync(context.CancellationToken);

        // Check which categories have children
        var categoryIds = categories.Select(c => c.Id).ToList();
        var childCounts = await db.Categories
            .Where(c => c.ParentId != null && categoryIds.Contains(c.ParentId.Value))
            .GroupBy(c => c.ParentId)
            .Select(g => new { ParentId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ParentId!.Value, x => x.Count, context.CancellationToken);

        var dtos = categories
            .Select(c => CategoryDto.FromEntity(c, childCounts.ContainsKey(c.Id)))
            .ToList();

        await context.RespondAsync(new GetRootCategoriesResult(dtos));
    }
}