using MassTransit;

namespace ProductInformationManager.Application.AttributeTypes;

public class GetAttributeTypeByIdConsumer(ProductContext db) : IConsumer<GetAttributeTypeById>
{
    public async Task Consume(ConsumeContext<GetAttributeTypeById> context)
    {
        var type = await db.AttributeTypes.FindAsync([context.Message.Id], context.CancellationToken);

        if (type is null)
        {
            await context.RespondAsync(new GetAttributeTypeByIdResult(null));
            return;
        }

        var attributes = await db.Attributes
            .Where(a => a.AttributeTypeId == type.Id)
            .OrderBy(a => a.Name)
            .Select(a => AttributeDto.FromEntity(a))
            .ToListAsync(context.CancellationToken);

        await context.RespondAsync(new GetAttributeTypeByIdResult(
            new AttributeTypeDto(type.Id, type.Name, type.Description, attributes)));
    }
}