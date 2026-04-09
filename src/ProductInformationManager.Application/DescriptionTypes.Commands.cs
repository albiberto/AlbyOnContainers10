using MassTransit;
using Microsoft.EntityFrameworkCore;
using ProductInformationManager.Domain;
using ProductInformationManager.Domain.Exceptions;
using ProductInformationManager.Domain.ValueObjects;
using ProductInformationManager.Infrastructure;
using ProductInformationManager.Messages;

namespace ProductInformationManager.Application;

public class CreateDescriptionTypeConsumer(ProductContext db) : IConsumer<CreateDescriptionType>
{
    public async Task Consume(ConsumeContext<CreateDescriptionType> context)
    {
        var command = context.Message;
        
        // Uso del Domain Model puro
        var entity = new DescriptionType(command.Name, command.Description);

        db.DescriptionTypes.Add(entity);
        await db.SaveChangesAsync(context.CancellationToken);

        await context.RespondAsync(new CreateDescriptionTypeResult(entity.Id.Value));
    }
}

public class UpdateDescriptionTypeConsumer(ProductContext db) : IConsumer<UpdateDescriptionType>
{
    public async Task Consume(ConsumeContext<UpdateDescriptionType> context)
    {
        var command = context.Message;
        var typeId = new DescriptionTypeId(command.Id);
        
        var entity = await db.DescriptionTypes.FindAsync([typeId], context.CancellationToken);

        if (entity is null)
        {
            await context.RespondAsync(new UpdateDescriptionTypeResult(false));
            return;
        }

        // Chiamata al metodo di Business del Dominio
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
            await context.RespondAsync(new DeleteDescriptionTypeResult(false));
            return;
        }

        db.DescriptionTypes.Remove(entity);
        await db.SaveChangesAsync(context.CancellationToken);
        
        await context.RespondAsync(new DeleteDescriptionTypeResult(true));
    }
}

public class AddDescriptionValueConsumer(ProductContext db) : IConsumer<AddDescriptionValue>
{
    public async Task Consume(ConsumeContext<AddDescriptionValue> context)
    {
        var command = context.Message;
        var typeId = new DescriptionTypeId(command.DescriptionTypeId);

        // Carichiamo l'Aggregato Root includendo i figli per le validazioni di dominio (es. no duplicati)
        var descriptionType = await db.DescriptionTypes
            .Include(d => d.Values)
            .FirstOrDefaultAsync(d => d.Id == typeId, context.CancellationToken);

        if (descriptionType is null)
            throw new DomainException($"DescriptionType {typeId.Value} non trovato.");

        // Deleghiamo all'Aggregato la creazione del figlio (protezione invarianti)
        descriptionType.AddValue(command.Value);
        
        await db.SaveChangesAsync(context.CancellationToken);

        // Recuperiamo l'ID appena generato
        var newValueId = descriptionType.Values.Last().Id.Value;

        await context.RespondAsync(new AddDescriptionValueResult(newValueId));
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
            await context.RespondAsync(new DeleteDescriptionValueResult(false));
            return;
        }

        // Rimozione diretta per questioni di pragmatismo dell'Infrastruttura
        db.DescriptionValues.Remove(entity);
        await db.SaveChangesAsync(context.CancellationToken);
        
        await context.RespondAsync(new DeleteDescriptionValueResult(true));
    }
}