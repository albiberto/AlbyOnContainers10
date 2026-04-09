using MassTransit;
using Microsoft.EntityFrameworkCore;
using ProductInformationManager.Domain.ValueObjects;
using ProductInformationManager.Infrastructure;
using ProductInformationManager.Messages;

namespace ProductInformationManager.Application;

public class GetDescriptionTypeByIdConsumer(ProductContext db) : IConsumer<GetDescriptionTypeById>
{
    public async Task Consume(ConsumeContext<GetDescriptionTypeById> context)
    {
        var typeId = new DescriptionTypeId(context.Message.Id);

        // Proiezione diretta: EF Core esegue una Left Join e restituisce solo ciò che ci serve
        var dto = await db.DescriptionTypes
            .AsNoTracking()
            .Where(d => d.Id == typeId)
            .Select(d => new DescriptionTypeDto(
                d.Id.Value,
                d.Name,
                d.Description,
                d.Values.Select(v => new DescriptionValueDto(v.Id.Value, v.Value)).ToList()
            ))
            .FirstOrDefaultAsync(context.CancellationToken);

        await context.RespondAsync(new GetDescriptionTypeByIdResult(dto));
    }
}

public class GetDescriptionTypesConsumer(ProductContext db) : IConsumer<GetDescriptionTypes>
{
    public async Task Consume(ConsumeContext<GetDescriptionTypes> context)
    {
        // Nessun Include, una sola query SQL ottimizzata con AsNoTracking
        var dtos = await db.DescriptionTypes
            .AsNoTracking()
            .OrderBy(d => d.Name)
            .Select(d => new DescriptionTypeDto(
                d.Id.Value,
                d.Name,
                d.Description,
                d.Values
                    .OrderBy(v => v.Value)
                    .Select(v => new DescriptionValueDto(v.Id.Value, v.Value))
                    .ToList()
            ))
            .ToListAsync(context.CancellationToken);

        await context.RespondAsync(new GetDescriptionTypesResult(dtos));
    }
}