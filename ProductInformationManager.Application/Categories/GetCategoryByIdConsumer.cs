using MassTransit;

namespace ProductInformationManager.Application.Categories;

public class GetCategoryByIdConsumer(ProductContext db) : IConsumer<GetCategoryById>
{
    public async Task Consume(ConsumeContext<GetCategoryById> context)
    {
        var category = await db.Categories.FindAsync([context.Message.Id], context.CancellationToken);
        var hasChildren = category is not null && await db.Categories.AnyAsync(c => c.ParentId == category.Id, context.CancellationToken);

        await context.RespondAsync(new GetCategoryByIdResult(
            category is not null ? CategoryDto.FromEntity(category, hasChildren) : null));
    }
}