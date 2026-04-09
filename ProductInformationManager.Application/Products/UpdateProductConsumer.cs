using MassTransit;

namespace ProductInformationManager.Application.Products;

public class UpdateProductConsumer(ProductContext db) : IConsumer<UpdateProduct>
{
    public async Task Consume(ConsumeContext<UpdateProduct> context)
    {
        var command = context.Message;
        var product = await db.Products.FindAsync([command.Id], context.CancellationToken);

        if (product is null)
        {
            await context.RespondAsync(new UpdateProductResult(false));
            return;
        }

        product.Update(command.Name, command.Sku, command.Description, command.Price, command.CategoryId, command.IsActive);

        await db.SaveChangesAsync(context.CancellationToken);
        await context.RespondAsync(new UpdateProductResult(true));
    }
}