using MassTransit;

namespace ProductInformationManager.Application.Categories;

public class UpdateCategoryConsumer(ProductContext db) : IConsumer<UpdateCategory>
{
    public async Task Consume(ConsumeContext<UpdateCategory> context)
    {
        var command = context.Message;
        var category = await db.Categories.FindAsync([command.Id], context.CancellationToken);

        if (category is null)
        {
            await context.RespondAsync(new UpdateCategoryResult(false));
            return;
        }

        category.Update(command.Name, command.Description);

        await db.SaveChangesAsync(context.CancellationToken);

        await context.RespondAsync(new UpdateCategoryResult(true));
    }
}