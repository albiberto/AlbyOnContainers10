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
// public class CreateDescriptionTypeConsumer(ProductContext db) : IConsumer<CreateDescriptionType>
// {
//     public async Task Consume(ConsumeContext<CreateDescriptionType> context)
//     {
//         var command = context.Message;
//
//         var entity = new DescriptionType(command.Name, command.Description);
//
//         db.DescriptionTypes.Add(entity);
//         await db.SaveChangesAsync(context.CancellationToken);
//
//         await context.RespondAsync(new CreateDescriptionTypeResult(true, entity.Id.Value));
//     }
// }
//
// public class UpdateDescriptionTypeConsumer(ProductContext db) : IConsumer<UpdateDescriptionType>
// {
//     public async Task Consume(ConsumeContext<UpdateDescriptionType> context)
//     {
//         var command = context.Message;
//
//         var typeId = new DescriptionTypeId(command.Id);
//         var entity = await db.DescriptionTypes.FindAsync([typeId], context.CancellationToken);
//
//         if (entity is null)
//         {
//             await context.RespondAsync(new UpdateDescriptionTypeResult(false, ValidationMessages.DescriptionTypeNotFound));
//             return;
//         }
//
//         entity.Rename(command.Name, command.Description);
//         
//         await db.SaveChangesAsync(context.CancellationToken);
//         await context.Publish(new DescriptionTypeUpdatedEvent(command.Id), context.CancellationToken);
//         await context.RespondAsync(new UpdateDescriptionTypeResult(true));
//     }
// }
//
// public class SetDescriptionTypeGlobalConsumer(ProductContext db) : IConsumer<SetDescriptionTypeGlobal>
// {
//     public async Task Consume(ConsumeContext<SetDescriptionTypeGlobal> context)
//     {
//         var command = context.Message;
//         var typeId = new DescriptionTypeId(command.Id);
//         
//         var entity = await db.DescriptionTypes.FindAsync([typeId], context.CancellationToken);
//         if (entity is null)
//         {
//             await context.RespondAsync(new SetDescriptionTypeGlobalResult(false, ValidationMessages.DescriptionTypeNotFound));
//             return;
//         }
//         //
//         // if (!command.IsGlobal)
//         // {
//         //     // If setting to false, we must ensure that no product outside of the explicit Category Rule uses this description.
//         //     var coveredCategoryIds = await db.CategoryDescriptionRules
//         //         .Where(r => r.DescriptionTypeId == typeId)
//         //         .Select(r => r.CategoryId)
//         //         .ToListAsync(context.CancellationToken);
//         //
//         //     // Fetch any product that uses a value of this DescriptionType AND is NOT in a covered category (or its descendants).
//         //     // Actually, a simple check: is there any product with this DescriptionType whose Category does NOT inherit the rule?
//         //     // To be precise, for each covered category we get all its descendants. 
//         //     // In EF Core and PostgreSQL LTree:
//         //     // This is a bit complex in LINQ. Let's get the covered paths:
//         //     var coveredCategories = await db.Categories
//         //         .Where(c => coveredCategoryIds.Contains(c.Id))
//         //         .ToListAsync(context.CancellationToken);
//         //
//         //     var query = db.Products.Where(p => 
//         //         db.Set<ProductDescription>().Any(pd => 
//         //             pd.ProductId == p.Id && 
//         //             db.DescriptionValues.Any(v => v.Id == pd.DescriptionValueId && v.DescriptionTypeId == typeId)
//         //         )
//         //     );
//         //
//         //     // Execute locally the filtering because IsDescendantOf might be hard to compose dynamically
//         //     var productsUsingDesc = await query
//         //         .Select(p => new { p.Sku, Path = p.Category.Path })
//         //         .ToListAsync(context.CancellationToken);
//         //
//         //     var invalidProducts = productsUsingDesc.Where(p => 
//         //         !coveredCategories.Any(c => p.Path.StartsWith(c.Path)) // LTree fallback check
//         //     ).ToList();
//         //
//         //     if (invalidProducts.Any())
//         //     {
//         //         var affectedSkus = invalidProducts.Select(p => p.Sku).Take(5).ToList();
//         //         string preview = string.Join(", ", affectedSkus);
//         //         string error = $"Impossibile rimuovere la globalità: i prodotti {preview} (e altri {invalidProducts.Count}) situati in categorie non direttamente associate usano ancora questa descrizione.";
//         //         await context.RespondAsync(new SetDescriptionTypeGlobalResult(false, error));
//         //         return;
//         //     }
//         // }
//         //
//         // entity.MakeGlobal(command.IsGlobal);
//         // await db.SaveChangesAsync(context.CancellationToken);
//         //
//         await context.Publish(new DescriptionTypeUpdatedEvent(command.Id), context.CancellationToken);
//         await context.RespondAsync(new SetDescriptionTypeGlobalResult(true));
//     }
// }
//
// public class DeleteDescriptionTypeConsumer(ProductContext db) : IConsumer<DeleteDescriptionType>
// {
//     public async Task Consume(ConsumeContext<DeleteDescriptionType> context)
//     {
//         var typeId = new DescriptionTypeId(context.Message.Id);
//         var entity = await db.DescriptionTypes.FindAsync([typeId], context.CancellationToken);
//
//         if (entity is null)
//         {
//             await context.RespondAsync(new DeleteDescriptionTypeResult(false, ValidationMessages.DescriptionTypeNotFound));
//             return;
//         }
//
//         // Check if any product has a description value of this type
//         var query = db.Products.Where(p => 
//             db.Set<ProductDescription>().Any(pd => 
//                 pd.ProductId == p.Id && 
//                 db.DescriptionValues.Any(v => v.Id == pd.DescriptionValueId && v.DescriptionTypeId == typeId)
//             )
//         );
//
//         var affectedSkus = await query.Select(p => p.Sku).Take(5).ToListAsync(context.CancellationToken);
//
//         if (affectedSkus.Count != 0)
//         {
//             var totalCount = await query.CountAsync(context.CancellationToken);
//             var preview = string.Join(", ", affectedSkus);
//             var error = $"Impossibile eliminare la descrizione. È attualmente compilata sui prodotti: {preview} e altri {totalCount}.";
//             await context.RespondAsync(new DeleteDescriptionTypeResult(false, error));
//             return;
//         }
//
//         db.DescriptionTypes.Remove(entity);
//         await db.SaveChangesAsync(context.CancellationToken);
//         
//         await context.RespondAsync(new DeleteDescriptionTypeResult(true));
//     }
// }
//
// public class AddDescriptionValueConsumer(ProductContext db) : IConsumer<AddDescriptionValue>
// {
//     public async Task Consume(ConsumeContext<AddDescriptionValue> context)
//     {
//         var command = context.Message;
//
//         var typeId = new DescriptionTypeId(command.DescriptionTypeId);
//
//         var descriptionType = await db.DescriptionTypes
//             .Include(d => d.Values)
//             .FirstOrDefaultAsync(d => d.Id == typeId, context.CancellationToken);
//
//         if (descriptionType is null)
//         {
//             await context.RespondAsync(new AddDescriptionValueResult(false, ErrorMessage: ValidationMessages.DescriptionTypeNotFound));
//             return;
//         }
//
//         descriptionType.AddValue(command.Value);
//         
//         await db.SaveChangesAsync(context.CancellationToken);
//
//         var newValueId = descriptionType.Values.Last().Id.Value;
//
//         await context.Publish(new DescriptionTypeUpdatedEvent(command.DescriptionTypeId), context.CancellationToken);
//         await context.RespondAsync(new AddDescriptionValueResult(true, newValueId));
//     }
// }
//
// public class DeleteDescriptionValueConsumer(ProductContext db) : IConsumer<DeleteDescriptionValue>
// {
//     public async Task Consume(ConsumeContext<DeleteDescriptionValue> context)
//     {
//         var valueId = new DescriptionValueId(context.Message.Id);
//         var entity = await db.DescriptionValues.FindAsync([valueId], context.CancellationToken);
//
//         if (entity is null)
//         {
//             await context.RespondAsync(new DeleteDescriptionValueResult(false, ValidationMessages.DescriptionValueDeleteInUse));
//             return;
//         }
//
//         // Check if value is used by any product
//         var query = db.Products.Where(p => 
//             db.Set<ProductDescription>().Any(pd => 
//                 pd.ProductId == p.Id && pd.DescriptionValueId == valueId
//             )
//         );
//
//         var affectedSkus = await query.Select(p => p.Sku).Take(5).ToListAsync(context.CancellationToken);
//
//         if (affectedSkus.Count != 0)
//         {
//             var totalCount = await query.CountAsync(context.CancellationToken);
//             var preview = string.Join(", ", affectedSkus);
//             var error = $"Impossibile eliminare il valore '{entity.Value}'. È attualmente assegnato ai prodotti: {preview} (e altri {totalCount}). Cambiare il valore su questi prodotti per procedere.";
//             await context.RespondAsync(new DeleteDescriptionValueResult(false, error));
//             return;
//         }
//
//         db.DescriptionValues.Remove(entity);
//         await db.SaveChangesAsync(context.CancellationToken);
//         
//         await context.Publish(new DescriptionTypeUpdatedEvent(entity.DescriptionTypeId.Value), context.CancellationToken);
//         await context.RespondAsync(new DeleteDescriptionValueResult(true));
//     }
// }
