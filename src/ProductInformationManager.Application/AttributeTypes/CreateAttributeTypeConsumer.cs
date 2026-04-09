using MassTransit;

namespace ProductInformationManager.Application.AttributeTypes;

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