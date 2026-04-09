using MassTransit;

namespace ProductInformationManager.Application.Categories;

public class DeleteCategoryConsumer(ProductContext db) : IConsumer<DeleteCategory>
{
    public async Task Consume(ConsumeContext<DeleteCategory> context)
    {
        var category = await db.Categories.FindAsync([context.Message.Id], context.CancellationToken);

        if (category is null)
        {
            await context.RespondAsync(new DeleteCategoryResult(false));
            return;
        }

        // Check if category has children
        var hasChildren = await db.Categories.AnyAsync(c => c.ParentId == category.Id, context.CancellationToken);
        if (hasChildren)
        {
            await context.RespondAsync(new DeleteCategoryResult(false));
            return;
        }

        db.Categories.Remove(category);
        await db.SaveChangesAsync(context.CancellationToken);

        await context.RespondAsync(new DeleteCategoryResult(true));
    }
}
