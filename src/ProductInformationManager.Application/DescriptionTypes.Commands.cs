using FluentValidation;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using ProductInformationManager.Application.Resources;
using ProductInformationManager.Domain;
using ProductInformationManager.Domain.ValueObjects;
using ProductInformationManager.Infrastructure;
using ProductInformationManager.Messages;

namespace ProductInformationManager.Application;

public class CreateDescriptionTypeConsumer(ProductContext db, IValidator<CreateDescriptionType> validator)
    : IConsumer<CreateDescriptionType>
{
    public async Task Consume(ConsumeContext<CreateDescriptionType> context)
    {
        var cmd = context.Message;
        var ct = context.CancellationToken;

        var validation = await validator.ValidateAsync(cmd, ct);
        if (!validation.IsValid)
        {
            await context.RespondAsync(new CreateDescriptionTypeResult(false, ErrorMessage: validation.Errors[0].ErrorMessage));
            return;
        }

        var entity = new DescriptionType(cmd.Name, cmd.Description, cmd.IsGlobal);
        db.DescriptionTypes.Add(entity);
        await db.SaveChangesAsync(ct);

        // If Specialized, associate the provided categories via CategoryDescriptionRule
        if (!cmd.IsGlobal)
        {
            foreach (var categoryId in cmd.CategoryIds.Distinct())
            {
                var catId = new CategoryId(categoryId);
                var category = await db.Categories
                    .Include(c => c.DescriptionRules)
                    .FirstOrDefaultAsync(c => c.Id == catId, ct);

                if (category is not null && !category.DescriptionRules.Any(r => r.DescriptionTypeId == entity.Id))
                    category.AddDescriptionRule(entity, isMandatory: false);
            }
            await db.SaveChangesAsync(ct);
        }

        await context.RespondAsync(new CreateDescriptionTypeResult(true, entity.Id.Value));
    }
}

public class UpdateDescriptionTypeConsumer(ProductContext db, IValidator<UpdateDescriptionType> validator)
    : IConsumer<UpdateDescriptionType>
{
    public async Task Consume(ConsumeContext<UpdateDescriptionType> context)
    {
        var cmd = context.Message;
        var ct = context.CancellationToken;

        var validation = await validator.ValidateAsync(cmd, ct);
        if (!validation.IsValid)
        {
            await context.RespondAsync(new UpdateDescriptionTypeResult(false, validation.Errors[0].ErrorMessage));
            return;
        }

        var typeId = new DescriptionTypeId(cmd.Id);

        var entity = await db.DescriptionTypes
            .FirstOrDefaultAsync(d => d.Id == typeId, ct);

        if (entity is null)
        {
            await context.RespondAsync(new UpdateDescriptionTypeResult(false, ValidationMessages.DescriptionTypeNotFound));
            return;
        }

        // Domain call — returns true if the type just became Global (invariant trigger)
        var becameGlobal = entity.UpdateMetadata(cmd.Name, cmd.Description, cmd.IsGlobal);

        if (becameGlobal)
        {
            // Invariant: remove all CategoryDescriptionRules for this type
            var rulesToDelete = await db.CategoryDescriptionRules
                .Where(r => r.DescriptionTypeId == typeId)
                .ToListAsync(ct);
            db.CategoryDescriptionRules.RemoveRange(rulesToDelete);
            await db.SaveChangesAsync(ct);
        }
        else if (!cmd.IsGlobal)
        {
            // Diff-based sync of category associations
            var currentRules = await db.CategoryDescriptionRules
                .Where(r => r.DescriptionTypeId == typeId)
                .ToListAsync(ct);

            var currentCategoryIds = currentRules.Select(r => r.CategoryId.Value).ToHashSet();
            var desiredCategoryIds = cmd.CategoryIds.ToHashSet();

            // Categories to remove
            var toRemove = currentRules
                .Where(r => !desiredCategoryIds.Contains(r.CategoryId.Value))
                .ToList();
            if (toRemove.Count > 0)
                db.CategoryDescriptionRules.RemoveRange(toRemove);

            // Categories to add
            var toAdd = desiredCategoryIds.Except(currentCategoryIds).ToList();
            foreach (var categoryId in toAdd)
            {
                var catId = new CategoryId(categoryId);
                var category = await db.Categories
                    .Include(c => c.DescriptionRules)
                    .FirstOrDefaultAsync(c => c.Id == catId, ct);

                if (category is not null && !category.DescriptionRules.Any(r => r.DescriptionTypeId == typeId))
                    category.AddDescriptionRule(entity, isMandatory: false);
            }

            await db.SaveChangesAsync(ct);
        }

        await db.SaveChangesAsync(ct);
        await context.RespondAsync(new UpdateDescriptionTypeResult(true));
    }
}

public class DeleteDescriptionTypeConsumer(ProductContext db) : IConsumer<DeleteDescriptionType>
{
    public async Task Consume(ConsumeContext<DeleteDescriptionType> context)
    {
        var ct = context.CancellationToken;
        var typeId = new DescriptionTypeId(context.Message.Id);

        var entity = await db.DescriptionTypes.FindAsync([typeId], ct);
        if (entity is null)
        {
            await context.RespondAsync(new DeleteDescriptionTypeResult(false, ValidationMessages.DescriptionTypeNotFound));
            return;
        }

        var isUsedByProducts = await db.DescriptionValues
            .Where(v => v.DescriptionTypeId == typeId)
            .SelectMany(v => db.Set<ProductDescription>().Where(pd => pd.DescriptionValueId == v.Id))
            .AnyAsync(ct);

        if (isUsedByProducts)
        {
            await context.RespondAsync(new DeleteDescriptionTypeResult(false, ValidationMessages.DescriptionTypeDeleteInUse));
            return;
        }

        db.DescriptionTypes.Remove(entity);
        await db.SaveChangesAsync(ct);
        await context.RespondAsync(new DeleteDescriptionTypeResult(true));
    }
}

public class AddDescriptionValueConsumer(ProductContext db, IValidator<AddDescriptionValue> validator)
    : IConsumer<AddDescriptionValue>
{
    public async Task Consume(ConsumeContext<AddDescriptionValue> context)
    {
        var cmd = context.Message;
        var ct = context.CancellationToken;

        var validation = await validator.ValidateAsync(cmd, ct);
        if (!validation.IsValid)
        {
            await context.RespondAsync(new AddDescriptionValueResult(false, ErrorMessage: validation.Errors[0].ErrorMessage));
            return;
        }

        var typeId = new DescriptionTypeId(cmd.DescriptionTypeId);
        var descriptionType = await db.DescriptionTypes
            .Include(d => d.Values)
            .FirstOrDefaultAsync(d => d.Id == typeId, ct);

        if (descriptionType is null)
        {
            await context.RespondAsync(new AddDescriptionValueResult(false, ErrorMessage: ValidationMessages.DescriptionTypeNotFound));
            return;
        }

        var previousCount = descriptionType.Values.Count;
        descriptionType.AddValue(cmd.Value);
        await db.SaveChangesAsync(ct);

        // Return the newly added value's ID (last item if count grew)
        var newValueId = descriptionType.Values.Count > previousCount
            ? descriptionType.Values.Last().Id.Value
            : Guid.Empty;

        await context.RespondAsync(new AddDescriptionValueResult(true, newValueId));
    }
}

public class DeleteDescriptionValueConsumer(ProductContext db) : IConsumer<DeleteDescriptionValue>
{
    public async Task Consume(ConsumeContext<DeleteDescriptionValue> context)
    {
        var ct = context.CancellationToken;
        var valueId = new DescriptionValueId(context.Message.Id);

        var entity = await db.DescriptionValues.FindAsync([valueId], ct);
        if (entity is null)
        {
            await context.RespondAsync(new DeleteDescriptionValueResult(false, ValidationMessages.DescriptionValueDeleteInUse));
            return;
        }

        var isUsedByProducts = await db.Set<ProductDescription>()
            .AnyAsync(pd => pd.DescriptionValueId == valueId, ct);

        if (isUsedByProducts)
        {
            await context.RespondAsync(new DeleteDescriptionValueResult(false, ValidationMessages.DescriptionValueDeleteInUse));
            return;
        }

        db.DescriptionValues.Remove(entity);
        await db.SaveChangesAsync(ct);
        await context.RespondAsync(new DeleteDescriptionValueResult(true));
    }
}
