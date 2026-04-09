using MassTransit;

namespace ProductInformationManager.Application.DescriptionTypes;

public class GetDescriptionTypeByIdConsumer(ProductContext db) : IConsumer<GetDescriptionTypeById>
{
    public async Task Consume(ConsumeContext<GetDescriptionTypeById> context)
    {
        var type = await db.DescriptionTypes
            .Include(d => d.Values)
            .FirstOrDefaultAsync(d => d.Id == context.Message.Id, context.CancellationToken);

        await context.RespondAsync(new GetDescriptionTypeByIdResult(
            type is not null ? DescriptionTypeDto.FromEntity(type) : null));
    }
}