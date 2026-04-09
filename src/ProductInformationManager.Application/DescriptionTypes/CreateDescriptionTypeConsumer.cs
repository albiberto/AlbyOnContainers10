using MassTransit;

namespace ProductInformationManager.Application.DescriptionTypes;

internal class CreateDescriptionTypeConsumer(ProductContext db) : IConsumer<CreateDescriptionType>
{
    public async Task Consume(ConsumeContext<CreateDescriptionType> context)
    {
        var command = context.Message;
        var entity = new DescriptionType(command.Name, command.Description);

        db.DescriptionTypes.Add(entity);
        await db.SaveChangesAsync(context.CancellationToken);

        await context.RespondAsync(new CreateDescriptionTypeResult(entity.Id));
    }
}