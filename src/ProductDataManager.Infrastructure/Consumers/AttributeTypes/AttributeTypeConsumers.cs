using MassTransit;
using Microsoft.EntityFrameworkCore;
using ProductDataManager.Infrastructure.Data;
using ProductDataManager.Infrastructure.Messages.AttributeTypes;
using ProductDataManager.Models;
using Attribute = ProductDataManager.Models.Attribute;

namespace ProductDataManager.Infrastructure.Consumers.AttributeTypes;

public class GetAttributeTypesConsumer(ProductContext db) : IConsumer<GetAttributeTypes>
{
    public async Task Consume(ConsumeContext<GetAttributeTypes> context)
    {
        var types = await db.AttributeTypes
            .OrderBy(a => a.Name)
            .ToListAsync(context.CancellationToken);

        // Load attributes for each type
        var typeIds = types.Select(t => t.Id).ToList();
        var attributes = await db.Attributes
            .Where(a => typeIds.Contains(a.AttributeTypeId))
            .OrderBy(a => a.Name)
            .ToListAsync(context.CancellationToken);

        var attributesByType = attributes.GroupBy(a => a.AttributeTypeId)
            .ToDictionary(g => g.Key, g => g.Select(AttributeDto.FromEntity).ToList());

        var dtos = types.Select(t =>
            new AttributeTypeDto(t.Id, t.Name, t.Description,
                attributesByType.GetValueOrDefault(t.Id, []))).ToList();

        await context.RespondAsync(new GetAttributeTypesResult(dtos));
    }
}

public class GetAttributeTypeByIdConsumer(ProductContext db) : IConsumer<GetAttributeTypeById>
{
    public async Task Consume(ConsumeContext<GetAttributeTypeById> context)
    {
        var type = await db.AttributeTypes.FindAsync([context.Message.Id], context.CancellationToken);

        if (type is null)
        {
            await context.RespondAsync(new GetAttributeTypeByIdResult(null));
            return;
        }

        var attributes = await db.Attributes
            .Where(a => a.AttributeTypeId == type.Id)
            .OrderBy(a => a.Name)
            .Select(a => AttributeDto.FromEntity(a))
            .ToListAsync(context.CancellationToken);

        await context.RespondAsync(new GetAttributeTypeByIdResult(
            new AttributeTypeDto(type.Id, type.Name, type.Description, attributes)));
    }
}

public class CreateAttributeTypeConsumer(ProductContext db) : IConsumer<CreateAttributeType>
{
    public async Task Consume(ConsumeContext<CreateAttributeType> context)
    {
        var command = context.Message;
        var entity = new AttributeType(command.Name, command.Description);

        db.AttributeTypes.Add(entity);
        await db.SaveChangesAsync(context.CancellationToken);

        await context.RespondAsync(new CreateAttributeTypeResult(entity.Id));
    }
}

public class UpdateAttributeTypeConsumer(ProductContext db) : IConsumer<UpdateAttributeType>
{
    public async Task Consume(ConsumeContext<UpdateAttributeType> context)
    {
        var command = context.Message;
        var entity = await db.AttributeTypes.FindAsync([command.Id], context.CancellationToken);

        if (entity is null)
        {
            await context.RespondAsync(new UpdateAttributeTypeResult(false));
            return;
        }

        entity.Name = command.Name;
        entity.Description = command.Description;
        await db.SaveChangesAsync(context.CancellationToken);

        await context.RespondAsync(new UpdateAttributeTypeResult(true));
    }
}

public class DeleteAttributeTypeConsumer(ProductContext db) : IConsumer<DeleteAttributeType>
{
    public async Task Consume(ConsumeContext<DeleteAttributeType> context)
    {
        var entity = await db.AttributeTypes.FindAsync([context.Message.Id], context.CancellationToken);

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

public class CreateAttributeConsumer(ProductContext db) : IConsumer<CreateAttribute>
{
    public async Task Consume(ConsumeContext<CreateAttribute> context)
    {
        var command = context.Message;
        var entity = new Attribute(command.Name, command.Value, command.AttributeTypeId);

        db.Attributes.Add(entity);
        await db.SaveChangesAsync(context.CancellationToken);

        await context.RespondAsync(new CreateAttributeResult(entity.Id));
    }
}

public class DeleteAttributeConsumer(ProductContext db) : IConsumer<DeleteAttribute>
{
    public async Task Consume(ConsumeContext<DeleteAttribute> context)
    {
        var entity = await db.Attributes.FindAsync([context.Message.Id], context.CancellationToken);

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
