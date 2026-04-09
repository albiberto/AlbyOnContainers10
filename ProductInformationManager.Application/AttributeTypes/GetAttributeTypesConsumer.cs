using MassTransit;

namespace ProductInformationManager.Application.AttributeTypes;

public class GetAttributeTypesConsumer(ProductContext db) : IConsumer<GetAttributeTypes>
{
    public async Task Consume(ConsumeContext<GetAttributeTypes> context)
    {
        var types = await db.AttributeTypes
            .OrderBy(a => a.Name)
            .ToListAsync(context.CancellationToken);

        // Load attributes for each type
        var typeIds = types.Select(t => t.Id).ToList();
        var attributes = await db.Attributes
            .Where(a => typeIds.Contains(a.AttributeTypeId))
            .OrderBy(a => a.Name)
            .ToListAsync(context.CancellationToken);

        var attributesByType = attributes.GroupBy(a => a.AttributeTypeId)
            .ToDictionary(g => g.Key, g => g.Select(AttributeDto.FromEntity).ToList());

        var dtos = types.Select(t =>
            new AttributeTypeDto(t.Id, t.Name, t.Description,
                attributesByType.GetValueOrDefault(t.Id, []))).ToList();

        await context.RespondAsync(new GetAttributeTypesResult(dtos));
    }
}