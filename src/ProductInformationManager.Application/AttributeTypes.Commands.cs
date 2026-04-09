using MassTransit;
using Microsoft.EntityFrameworkCore;
using ProductInformationManager.Domain;
using ProductInformationManager.Domain.Exceptions;
using ProductInformationManager.Domain.ValueObjects;
using ProductInformationManager.Infrastructure;
using ProductInformationManager.Messages;

namespace ProductInformationManager.Application;

public class CreateAttributeTypeConsumer(ProductContext db) : IConsumer<CreateAttributeType>
{
    public async Task Consume(ConsumeContext<CreateAttributeType> context)
    {
        var command = context.Message;
        
        // Uso del Domain Model puro (l'ID viene generato internamente dalla factory)
        var entity = new AttributeType(command.Name, command.Description);

        db.AttributeTypes.Add(entity);
        await db.SaveChangesAsync(context.CancellationToken);

        // Estrazione del valore primitivo .Value per il messaggio di risposta
        await context.RespondAsync(new CreateAttributeTypeResult(entity.Id.Value));
    }
}

public class UpdateAttributeTypeConsumer(ProductContext db) : IConsumer<UpdateAttributeType>
{
    public async Task Consume(ConsumeContext<UpdateAttributeType> context)
    {
        var command = context.Message;
        var typeId = new AttributeTypeId(command.Id); // Cast Strongly-Typed
        
        var entity = await db.AttributeTypes.FindAsync([typeId], context.CancellationToken);

        if (entity is null)
        {
            await context.RespondAsync(new UpdateAttributeTypeResult(false));
            return;
        }

        // Passaggio per il metodo di business del Dominio
        entity.Rename(command.Name, command.Description);
        
        await db.SaveChangesAsync(context.CancellationToken);
        await context.RespondAsync(new UpdateAttributeTypeResult(true));
    }
}

public class CreateAttributeConsumer(ProductContext db) : IConsumer<CreateAttribute>
{
    public async Task Consume(ConsumeContext<CreateAttribute> context)
    {
        var command = context.Message;
        var typeId = new AttributeTypeId(command.AttributeTypeId);

        // Carichiamo l'Aggregato Root includendo i figli per validare l'idempotenza
        var attributeType = await db.AttributeTypes
            .Include(at => at.Attributes)
            .FirstOrDefaultAsync(at => at.Id == typeId, context.CancellationToken);

        if (attributeType is null)
            throw new DomainException($"AttributeType {typeId.Value} non trovato.");

        // NON usiamo new Attribute(). Deleghiamo all'Aggregato la creazione!
        attributeType.AddAttribute(command.Name, command.Value);
        
        await db.SaveChangesAsync(context.CancellationToken);

        // Recuperiamo l'ultimo inserito per restituirne l'ID
        var newAttrId = attributeType.Attributes.Last().Id.Value;

        await context.RespondAsync(new CreateAttributeResult(newAttrId));
    }
}

public class DeleteAttributeConsumer(ProductContext db) : IConsumer<DeleteAttribute>
{
    public async Task Consume(ConsumeContext<DeleteAttribute> context)
    {
        var attributeId = new AttributeId(context.Message.Id);
        var entity = await db.Attributes.FindAsync([attributeId], context.CancellationToken);

        if (entity is null)
        {
            await context.RespondAsync(new DeleteAttributeResult(false));
            return;
        }

        db.Attributes.Remove(entity);
        await db.SaveChangesAsync(context.CancellationToken);
        await context.RespondAsync(new DeleteAttributeResult(true));
    }
}

public class DeleteAttributeTypeConsumer(ProductContext db) : IConsumer<DeleteAttributeType>
{
    public async Task Consume(ConsumeContext<DeleteAttributeType> context)
    {
        var typeId = new AttributeTypeId(context.Message.Id);
        var entity = await db.AttributeTypes.FindAsync([typeId], context.CancellationToken);

        if (entity is null)
        {
            await context.RespondAsync(new DeleteAttributeTypeResult(false));
            return;
        }

        db.AttributeTypes.Remove(entity);
        await db.SaveChangesAsync(context.CancellationToken);
        await context.RespondAsync(new DeleteAttributeTypeResult(true));
    }
}