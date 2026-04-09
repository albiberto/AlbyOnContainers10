using MassTransit;

namespace ProductInformationManager.Application.Products;

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