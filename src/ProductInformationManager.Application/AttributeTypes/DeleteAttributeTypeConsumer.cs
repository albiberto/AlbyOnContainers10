using MassTransit;

namespace ProductInformationManager.Application.AttributeTypes;

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