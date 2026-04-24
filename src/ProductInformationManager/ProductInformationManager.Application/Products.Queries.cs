// using MassTransit;
// using Microsoft.EntityFrameworkCore;
// using ProductInformationManager.Domain.ValueObjects;
// using ProductInformationManager.Infrastructure;
// using ProductInformationManager.Messages;
//
// namespace ProductInformationManager.Application;
//
// public class GetProductByIdConsumer(ProductContext db) : IConsumer<GetProductById>
// {
//     public async Task Consume(ConsumeContext<GetProductById> context)
//     {
//         var productId = new ProductId(context.Message.Id);
//
//         var dto = await db.Products
//             .AsNoTracking()
//             .Where(p => p.Id == productId)
//             .Select(p => new ProductDto(
//                 p.Id.Value,
//                 p.Name,
//                 p.Sku,
//                 p.Description,
//                 p.IsActive,
//                 p.CategoryId.Value,
//                 p.Category.Name,
//                 // Navigazione nativa senza join esplicite
//                 p.Attributes.Select(a => new ProductAttributeDto(
//                     a.Id.Value,
//                     a.Name,
//                     a.Value,
//                     a.AttributeType.Name
//                 )).ToList(),
//                 // Proiezione della nuova collection Descriptions
//                 p.Descriptions.Select(d => new ProductDescriptionDto(
//                     d.DescriptionType.Id.Value,
//                     d.DescriptionType.Name,
//                     d.Id.Value,
//                     d.Value
//                 )).ToList()
//             ))
//             .FirstOrDefaultAsync(context.CancellationToken);
//
//         await context.RespondAsync(new GetProductByIdResult(dto));
//     }
// }
//
// public class GetProductsConsumer(ProductContext db) : IConsumer<GetProducts>
// {
//     public async Task Consume(ConsumeContext<GetProducts> context)
//     {
//         var query = db.Products.AsNoTracking().AsQueryable();
//
//         if (context.Message.CategoryId.HasValue)
//         {
//             var categoryId = new CategoryId(context.Message.CategoryId.Value);
//             query = query.Where(p => p.CategoryId == categoryId);
//         }
//
//         if (context.Message.IsActive.HasValue)
//         {
//             query = query.Where(p => p.IsActive == context.Message.IsActive);
//         }
//
//         var totalCount = await query.CountAsync(context.CancellationToken);
//
//         var dtos = await query
//             .OrderBy(p => p.Name)
//             .Skip((context.Message.Page - 1) * context.Message.PageSize)
//             .Take(context.Message.PageSize)
//             .Select(p => new ProductDto(
//                 p.Id.Value,
//                 p.Name,
//                 p.Sku,
//                 p.Description,
//                 p.IsActive,
//                 p.CategoryId.Value,
//                 p.Category.Name,
//                 p.Attributes.Select(a => new ProductAttributeDto(
//                     a.Id.Value,
//                     a.Name,
//                     a.Value,
//                     a.AttributeType.Name
//                 )).ToList(),
//                 p.Descriptions.Select(d => new ProductDescriptionDto(
//                     d.DescriptionType.Id.Value,
//                     d.DescriptionType.Name,
//                     d.Id.Value,
//                     d.Value
//                 )).ToList()
//             ))
//             .ToListAsync(context.CancellationToken);
//
//         await context.RespondAsync(new GetProductsResult(dtos, totalCount));
//     }
// }