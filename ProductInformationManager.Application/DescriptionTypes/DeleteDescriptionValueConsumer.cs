using MassTransit;

namespace ProductInformationManager.Application.DescriptionTypes;

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
