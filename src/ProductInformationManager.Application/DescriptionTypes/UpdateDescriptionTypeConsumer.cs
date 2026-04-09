using MassTransit;

namespace ProductInformationManager.Application.DescriptionTypes;

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

        entity.Update(command.Name, command.Description);
        await db.SaveChangesAsync(context.CancellationToken);

        await context.RespondAsync(new UpdateDescriptionTypeResult(true));
    }
}