using MassTransit;

namespace ProductInformationManager.Application.Products;

public class DeleteProductConsumer(ProductContext db) : IConsumer<DeleteProduct>
{
    public async Task Consume(ConsumeContext<DeleteProduct> context)
    {
        var product = await db.Products.FindAsync([context.Message.Id], context.CancellationToken);

        if (product is null)
        {
            await context.RespondAsync(new DeleteProductResult(false));
            return;
        }

        db.Products.Remove(product);
        await db.SaveChangesAsync(context.CancellationToken);
        await context.RespondAsync(new DeleteProductResult(true));
    }
}
