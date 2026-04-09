using MassTransit;

namespace ProductInformationManager.Application.DescriptionTypes;

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