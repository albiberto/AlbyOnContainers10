using MassTransit;

namespace ProductInformationManager.Application.Categories;

public class GetChildCategoriesConsumer(ProductContext db) : IConsumer<GetChildCategories>
{
    public async Task Consume(ConsumeContext<GetChildCategories> context)
    {
        var parentId = context.Message.ParentId;

        var children = await db.Categories
            .Where(c => c.ParentId == parentId)
            .OrderBy(c => c.Name)
            .ToListAsync(context.CancellationToken);

        var childIds = children.Select(c => c.Id).ToList();
        var grandchildCounts = await db.Categories
            .Where(c => c.ParentId != null && childIds.Contains(c.ParentId.Value))
            .GroupBy(c => c.ParentId)
            .Select(g => new { ParentId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ParentId!.Value, x => x.Count, context.CancellationToken);

        var dtos = children
            .Select(c => CategoryDto.FromEntity(c, grandchildCounts.ContainsKey(c.Id)))
            .ToList();

        await context.RespondAsync(new GetChildCategoriesResult(dtos));
    }
}