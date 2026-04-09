using MassTransit;

namespace ProductInformationManager.Application.Products;

public class CreateProductConsumer(ProductContext db) : IConsumer<CreateProduct>
{
    public async Task Consume(ConsumeContext<CreateProduct> context)
    {
        var command = context.Message;
        var product = new ProductInformationManager.Domain.Product(command.Name, command.Sku, command.CategoryId, command.Price, command.Description);

        db.Products.Add(product);
        await db.SaveChangesAsync(context.CancellationToken);

        await context.RespondAsync(new CreateProductResult(product.Id));
    }
}