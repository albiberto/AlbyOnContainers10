// using FluentValidation;
// using MassTransit;
// using Microsoft.EntityFrameworkCore;
// using ProductInformationManager.Application.Resources;
// using ProductInformationManager.Domain;
// using ProductInformationManager.Domain.Exceptions;
// using ProductInformationManager.Domain.ValueObjects;
// using ProductInformationManager.Infrastructure;
// using ProductInformationManager.Messages;
//
// namespace ProductInformationManager.Application;
//
// public class CreateAttributeTypeConsumer(ProductContext db, IValidator<CreateAttributeType> validator) : IConsumer<CreateAttributeType>
// {
//     public async Task Consume(ConsumeContext<CreateAttributeType> context)
//     {
//         var command = context.Message;
//
//         var validation = await validator.ValidateAsync(command, context.CancellationToken);
//         if (!validation.IsValid)
//         {
//             await context.RespondAsync(new CreateAttributeTypeResult(false, ErrorMessage: validation.Errors[0].ErrorMessage));
//             return;
//         }
//
//         var entity = new AttributeType(command.Name, command.Description);
//
//         db.AttributeTypes.Add(entity);
//         await db.SaveChangesAsync(context.CancellationToken);
//
//         await context.RespondAsync(new CreateAttributeTypeResult(true, entity.Id.Value));
//     }
// }
//
// public class UpdateAttributeTypeConsumer(ProductContext db, IValidator<UpdateAttributeType> validator) : IConsumer<UpdateAttributeType>
// {
//     public async Task Consume(ConsumeContext<UpdateAttributeType> context)
//     {
//         var command = context.Message;
//
//         var validation = await validator.ValidateAsync(command, context.CancellationToken);
//         if (!validation.IsValid)
//         {
//             await context.RespondAsync(new UpdateAttributeTypeResult(false, validation.Errors[0].ErrorMessage));
//             return;
//         }
//
//         var typeId = new AttributeTypeId(command.Id);
//         var entity = await db.AttributeTypes.FindAsync([typeId], context.CancellationToken);
//
//         if (entity is null)
//         {
//             await context.RespondAsync(new UpdateAttributeTypeResult(false, ValidationMessages.AttributeTypeNotFound));
//             return;
//         }
//
//         entity.Rename(command.Name, command.Description);
//         
//         await db.SaveChangesAsync(context.CancellationToken);
//         await context.RespondAsync(new UpdateAttributeTypeResult(true));
//     }
// }
//
// public class CreateAttributeConsumer(ProductContext db, IValidator<CreateAttribute> validator) : IConsumer<CreateAttribute>
// {
//     public async Task Consume(ConsumeContext<CreateAttribute> context)
//     {
//         var command = context.Message;
//
//         var validation = await validator.ValidateAsync(command, context.CancellationToken);
//         if (!validation.IsValid)
//         {
//             await context.RespondAsync(new CreateAttributeResult(false, ErrorMessage: validation.Errors[0].ErrorMessage));
//             return;
//         }
//
//         var typeId = new AttributeTypeId(command.AttributeTypeId);
//
//         var attributeType = await db.AttributeTypes
//             .Include(at => at.Attributes)
//             .FirstOrDefaultAsync(at => at.Id == typeId, context.CancellationToken);
//
//         if (attributeType is null)
//         {
//             await context.RespondAsync(new CreateAttributeResult(false, ErrorMessage: ValidationMessages.AttributeTypeNotFound));
//             return;
//         }
//
//         attributeType.AddAttribute(command.Name, command.Value);
//         
//         await db.SaveChangesAsync(context.CancellationToken);
//
//         var newAttrId = attributeType.Attributes.Last().Id.Value;
//
//         await context.RespondAsync(new CreateAttributeResult(true, newAttrId));
//     }
// }
//
// public class DeleteAttributeConsumer(ProductContext db) : IConsumer<DeleteAttribute>
// {
//     public async Task Consume(ConsumeContext<DeleteAttribute> context)
//     {
//         var attributeId = new AttributeId(context.Message.Id);
//         var entity = await db.Attributes.FindAsync([attributeId], context.CancellationToken);
//
//         if (entity is null)
//         {
//             await context.RespondAsync(new DeleteAttributeResult(false, ValidationMessages.AttributeTypeNotFound));
//             return;
//         }
//
//         // Check if attribute is used by any product
//         var query = db.Products.Where(p => 
//             db.Set<ProductAttribute>().Any(pa => pa.ProductId == p.Id && pa.AttributeId == attributeId)
//         );
//
//         var affectedSkus = await query.Select(p => p.Sku).Take(5).ToListAsync(context.CancellationToken);
//
//         if (affectedSkus.Count != 0)
//         {
//             var totalCount = await query.CountAsync(context.CancellationToken);
//             var preview = string.Join(", ", affectedSkus);
//             var error = $"Impossibile eliminare l'attributo: è assegnato ai prodotti {preview} (e altri {totalCount}).";
//             await context.RespondAsync(new DeleteAttributeResult(false, error));
//             return;
//         }
//
//         db.Attributes.Remove(entity);
//         await db.SaveChangesAsync(context.CancellationToken);
//         await context.RespondAsync(new DeleteAttributeResult(true));
//     }
// }
//
// public class DeleteAttributeTypeConsumer(ProductContext db) : IConsumer<DeleteAttributeType>
// {
//     public async Task Consume(ConsumeContext<DeleteAttributeType> context)
//     {
//         var typeId = new AttributeTypeId(context.Message.Id);
//         var entity = await db.AttributeTypes.FindAsync([typeId], context.CancellationToken);
//
//         if (entity is null)
//         {
//             await context.RespondAsync(new DeleteAttributeTypeResult(false, ValidationMessages.AttributeTypeNotFound));
//             return;
//         }
//
//         // Check if any attributes of this type are used by products
//         var query = db.Products.Where(p => 
//             db.Set<ProductAttribute>().Any(pa => 
//                 pa.ProductId == p.Id && 
//                 db.Attributes.Any(a => a.Id == pa.AttributeId && a.AttributeTypeId == typeId)
//             )
//         );
//
//         var affectedSkus = await query.Select(p => p.Sku).Take(5).ToListAsync(context.CancellationToken);
//
//         if (affectedSkus.Count != 0)
//         {
//             var totalCount = await query.CountAsync(context.CancellationToken);
//             var preview = string.Join(", ", affectedSkus);
//             var error = $"Impossibile eliminare l'attributo globale: è in uso sui prodotti {preview} (e altri {totalCount}).";
//             await context.RespondAsync(new DeleteAttributeTypeResult(false, error));
//             return;
//         }
//
//         db.AttributeTypes.Remove(entity);
//         await db.SaveChangesAsync(context.CancellationToken);
//         await context.RespondAsync(new DeleteAttributeTypeResult(true));
//     }
// }
