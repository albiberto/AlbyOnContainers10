using MassTransit;

namespace ProductInformationManager.Application.AttributeTypes;

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

        entity.Update(command.Name, command.Description);
        await db.SaveChangesAsync(context.CancellationToken);

        await context.RespondAsync(new UpdateAttributeTypeResult(true));
    }
}