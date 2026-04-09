using MassTransit;
using ProductInformationManager.Infrastructure;
using ProductInformationManager.Messages.AttributeTypes;
using Attribute = ProductInformationManager.Domain.Attribute; 

namespace ProductInformationManager.Application.AttributeTypes;

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