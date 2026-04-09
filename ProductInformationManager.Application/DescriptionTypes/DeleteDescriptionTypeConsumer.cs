using MassTransit;

namespace ProductInformationManager.Application.DescriptionTypes;

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