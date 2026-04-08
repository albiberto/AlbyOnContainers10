using MassTransit;
using Microsoft.EntityFrameworkCore;
using ProductDataManager.Infrastructure.Data;
using ProductDataManager.Infrastructure.Messages.Products;

namespace ProductDataManager.Infrastructure.Consumers.Products;

public class GetProductsConsumer(ProductContext db) : IConsumer<GetProducts>
{
    public async Task Consume(ConsumeContext<GetProducts> context)
    {
        var query = db.Products
            .Include(p => p.Category)
            .Include(p => p.ProductAttributes)
                .ThenInclude(pa => pa.Attribute)
                    .ThenInclude(a => a.AttributeType)
            .AsQueryable();

        if (context.Message.CategoryId is not null)
            query = query.Where(p => p.CategoryId == context.Message.CategoryId);

        if (context.Message.IsActive is not null)
            query = query.Where(p => p.IsActive == context.Message.IsActive);

        var totalCount = await query.CountAsync(context.CancellationToken);

        var products = await query
            .OrderBy(p => p.Name)
            .Skip((context.Message.Page - 1) * context.Message.PageSize)
            .Take(context.Message.PageSize)
            .ToListAsync(context.CancellationToken);

        var dtos = products.Select(ProductDto.FromEntity).ToList();

        await context.RespondAsync(new GetProductsResult(dtos, totalCount));
    }
}

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

public class CreateProductConsumer(ProductContext db) : IConsumer<CreateProduct>
{
    public async Task Consume(ConsumeContext<CreateProduct> context)
    {
        var command = context.Message;
        var product = new ProductDataManager.Models.Product(command.Name, command.Sku, command.CategoryId, command.Price, command.Description);

        db.Products.Add(product);
        await db.SaveChangesAsync(context.CancellationToken);

        await context.RespondAsync(new CreateProductResult(product.Id));
    }
}

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
