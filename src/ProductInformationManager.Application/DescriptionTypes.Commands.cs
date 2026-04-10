using FluentValidation;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using ProductInformationManager.Application.Resources;
using ProductInformationManager.Domain;
using ProductInformationManager.Domain.Exceptions;
using ProductInformationManager.Domain.ValueObjects;
using ProductInformationManager.Infrastructure;
using ProductInformationManager.Messages;

namespace ProductInformationManager.Application;

public class CreateDescriptionTypeConsumer(ProductContext db, IValidator<CreateDescriptionType> validator) : IConsumer<CreateDescriptionType>
{
    public async Task Consume(ConsumeContext<CreateDescriptionType> context)
    {
        var command = context.Message;

        var validation = await validator.ValidateAsync(command, context.CancellationToken);
        if (!validation.IsValid)
        {
            await context.RespondAsync(new CreateDescriptionTypeResult(false, ErrorMessage: validation.Errors[0].ErrorMessage));
            return;
        }

        var entity = new DescriptionType(command.Name, command.Description);

        db.DescriptionTypes.Add(entity);
        await db.SaveChangesAsync(context.CancellationToken);

        await context.RespondAsync(new CreateDescriptionTypeResult(true, entity.Id.Value));
    }
}

public class UpdateDescriptionTypeConsumer(ProductContext db, IValidator<UpdateDescriptionType> validator) : IConsumer<UpdateDescriptionType>
{
    public async Task Consume(ConsumeContext<UpdateDescriptionType> context)
    {
        var command = context.Message;

        var validation = await validator.ValidateAsync(command, context.CancellationToken);
        if (!validation.IsValid)
        {
            await context.RespondAsync(new UpdateDescriptionTypeResult(false, validation.Errors[0].ErrorMessage));
            return;
        }

        var typeId = new DescriptionTypeId(command.Id);
        var entity = await db.DescriptionTypes.FindAsync([typeId], context.CancellationToken);

        if (entity is null)
        {
            await context.RespondAsync(new UpdateDescriptionTypeResult(false, ValidationMessages.DescriptionTypeNotFound));
            return;
        }

        entity.Rename(command.Name, command.Description);
        
        await db.SaveChangesAsync(context.CancellationToken);
        await context.RespondAsync(new UpdateDescriptionTypeResult(true));
    }
}

public class DeleteDescriptionTypeConsumer(ProductContext db) : IConsumer<DeleteDescriptionType>
{
    public async Task Consume(ConsumeContext<DeleteDescriptionType> context)
    {
        var typeId = new DescriptionTypeId(context.Message.Id);
        var entity = await db.DescriptionTypes.FindAsync([typeId], context.CancellationToken);

        if (entity is null)
        {
            await context.RespondAsync(new DeleteDescriptionTypeResult(false, ValidationMessages.DescriptionTypeNotFound));
            return;
        }

        // Check if any product has a description value of this type
        var isUsedByProducts = await db.DescriptionValues
            .Where(v => v.DescriptionTypeId == typeId)
            .SelectMany(v => db.Set<ProductDescription>().Where(pd => pd.DescriptionValueId == v.Id))
            .AnyAsync(context.CancellationToken);

        if (isUsedByProducts)
        {
            await context.RespondAsync(new DeleteDescriptionTypeResult(false, ValidationMessages.DescriptionTypeDeleteInUse));
            return;
        }

        db.DescriptionTypes.Remove(entity);
        await db.SaveChangesAsync(context.CancellationToken);
        
        await context.RespondAsync(new DeleteDescriptionTypeResult(true));
    }
}

public class AddDescriptionValueConsumer(ProductContext db, IValidator<AddDescriptionValue> validator) : IConsumer<AddDescriptionValue>
{
    public async Task Consume(ConsumeContext<AddDescriptionValue> context)
    {
        var command = context.Message;

        var validation = await validator.ValidateAsync(command, context.CancellationToken);
        if (!validation.IsValid)
        {
            await context.RespondAsync(new AddDescriptionValueResult(false, ErrorMessage: validation.Errors[0].ErrorMessage));
            return;
        }

        var typeId = new DescriptionTypeId(command.DescriptionTypeId);

        var descriptionType = await db.DescriptionTypes
            .Include(d => d.Values)
            .FirstOrDefaultAsync(d => d.Id == typeId, context.CancellationToken);

        if (descriptionType is null)
        {
            await context.RespondAsync(new AddDescriptionValueResult(false, ErrorMessage: ValidationMessages.DescriptionTypeNotFound));
            return;
        }

        descriptionType.AddValue(command.Value);
        
        await db.SaveChangesAsync(context.CancellationToken);

        var newValueId = descriptionType.Values.Last().Id.Value;

        await context.RespondAsync(new AddDescriptionValueResult(true, newValueId));
    }
}

public class DeleteDescriptionValueConsumer(ProductContext db) : IConsumer<DeleteDescriptionValue>
{
    public async Task Consume(ConsumeContext<DeleteDescriptionValue> context)
    {
        var valueId = new DescriptionValueId(context.Message.Id);
        var entity = await db.DescriptionValues.FindAsync([valueId], context.CancellationToken);

        if (entity is null)
        {
            await context.RespondAsync(new DeleteDescriptionValueResult(false, ValidationMessages.DescriptionValueDeleteInUse));
            return;
        }

        // Check if value is used by any product
        var isUsedByProducts = await db.Set<ProductDescription>()
            .AnyAsync(pd => pd.DescriptionValueId == valueId, context.CancellationToken);

        if (isUsedByProducts)
        {
            await context.RespondAsync(new DeleteDescriptionValueResult(false, ValidationMessages.DescriptionValueDeleteInUse));
            return;
        }

        db.DescriptionValues.Remove(entity);
        await db.SaveChangesAsync(context.CancellationToken);
        
        await context.RespondAsync(new DeleteDescriptionValueResult(true));
    }
}
