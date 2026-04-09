using MassTransit;

namespace ProductInformationManager.Application.Products;

public class GetProductByIdConsumer(ProductContext db) : IConsumer<GetProductById>
{
    public async Task Consume(ConsumeContext<GetProductById> context)
    {
        var product = await db.Products
            .Include(p => p.Category)
            .Include(p => p.ProductAttributes)
            .ThenInclude(pa => pa.Attribute)
            .ThenInclude(a => a.AttributeType)
            .FirstOrDefaultAsync(p => p.Id == context.Message.Id, context.CancellationToken);

        await context.RespondAsync(new GetProductByIdResult(
            product is not null ? ProductDto.FromEntity(product) : null));
    }
}