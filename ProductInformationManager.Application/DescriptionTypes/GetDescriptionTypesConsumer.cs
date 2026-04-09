using MassTransit;

namespace ProductInformationManager.Application.DescriptionTypes;

public class GetDescriptionTypesConsumer(ProductContext db) : IConsumer<GetDescriptionTypes>
{
    public async Task Consume(ConsumeContext<GetDescriptionTypes> context)
    {
        var types = await db.DescriptionTypes
            .Include(d => d.Values)
            .OrderBy(d => d.Name)
            .ToListAsync(context.CancellationToken);

        var dtos = types.Select(DescriptionTypeDto.FromEntity).ToList();
        await context.RespondAsync(new GetDescriptionTypesResult(dtos));
    }
}