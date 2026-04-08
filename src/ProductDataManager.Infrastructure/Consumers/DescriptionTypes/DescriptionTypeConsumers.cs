using MassTransit;
using Microsoft.EntityFrameworkCore;
using ProductDataManager.Infrastructure.Data;
using ProductDataManager.Infrastructure.Messages.DescriptionTypes;
using ProductDataManager.Models;

namespace ProductDataManager.Infrastructure.Consumers.DescriptionTypes;

public class GetDescriptionTypesConsumer(ProductContext db) : IConsumer<GetDescriptionTypes>
{
    public async Task Consume(ConsumeContext<GetDescriptionTypes> context)
    {
        var types = await db.DescriptionTypes
            .Include(d => d.Values)
            .OrderBy(d => d.Name)
            .ToListAsync(context.CancellationToken);

        var dtos = types.Select(DescriptionTypeDto.FromEntity).ToList();
        await context.RespondAsync(new GetDescriptionTypesResult(dtos));
    }
}

public class GetDescriptionTypeByIdConsumer(ProductContext db) : IConsumer<GetDescriptionTypeById>
{
    public async Task Consume(ConsumeContext<GetDescriptionTypeById> context)
    {
        var type = await db.DescriptionTypes
            .Include(d => d.Values)
            .FirstOrDefaultAsync(d => d.Id == context.Message.Id, context.CancellationToken);

        await context.RespondAsync(new GetDescriptionTypeByIdResult(
            type is not null ? DescriptionTypeDto.FromEntity(type) : null));
    }
}

public class CreateDescriptionTypeConsumer(ProductContext db) : IConsumer<CreateDescriptionType>
{
    public async Task Consume(ConsumeContext<CreateDescriptionType> context)
    {
        var command = context.Message;
        var entity = new DescriptionType(command.Name, command.Description);

        db.DescriptionTypes.Add(entity);
        await db.SaveChangesAsync(context.CancellationToken);

        await context.RespondAsync(new CreateDescriptionTypeResult(entity.Id));
    }
}

public class UpdateDescriptionTypeConsumer(ProductContext db) : IConsumer<UpdateDescriptionType>
{
    public async Task Consume(ConsumeContext<UpdateDescriptionType> context)
    {
        var command = context.Message;
        var entity = await db.DescriptionTypes.FindAsync([command.Id], context.CancellationToken);

        if (entity is null)
        {
            await context.RespondAsync(new UpdateDescriptionTypeResult(false));
            return;
        }

        entity.Name = command.Name;
        entity.Description = command.Description;
        await db.SaveChangesAsync(context.CancellationToken);

        await context.RespondAsync(new UpdateDescriptionTypeResult(true));
    }
}

public class DeleteDescriptionTypeConsumer(ProductContext db) : IConsumer<DeleteDescriptionType>
{
    public async Task Consume(ConsumeContext<DeleteDescriptionType> context)
    {
        var entity = await db.DescriptionTypes.FindAsync([context.Message.Id], context.CancellationToken);

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
        var entity = new DescriptionValue(command.Value, command.DescriptionTypeId);

        db.DescriptionValues.Add(entity);
        await db.SaveChangesAsync(context.CancellationToken);

        await context.RespondAsync(new AddDescriptionValueResult(entity.Id));
    }
}

public class DeleteDescriptionValueConsumer(ProductContext db) : IConsumer<DeleteDescriptionValue>
{
    public async Task Consume(ConsumeContext<DeleteDescriptionValue> context)
    {
        var entity = await db.DescriptionValues.FindAsync([context.Message.Id], context.CancellationToken);

        if (entity is null)
        {
            await context.RespondAsync(new DeleteDescriptionValueResult(false));
            return;
        }

        db.DescriptionValues.Remove(entity);
        await db.SaveChangesAsync(context.CancellationToken);
        await context.RespondAsync(new DeleteDescriptionValueResult(true));
    }
}
