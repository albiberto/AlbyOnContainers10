using MassTransit;
using ProductInformationManager.Infrastructure;
using ProductInformationManager.Messages.AttributeTypes;

namespace ProductInformationManager.Application.AttributeTypes;

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
