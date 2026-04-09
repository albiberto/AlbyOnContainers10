using MassTransit;
using Microsoft.EntityFrameworkCore;
using ProductInformationManager.Domain.ValueObjects;
using ProductInformationManager.Infrastructure;
using ProductInformationManager.Messages;

namespace ProductInformationManager.Application;

public class GetAttributeTypeByIdConsumer(ProductContext db) : IConsumer<GetAttributeTypeById>
{
    public async Task Consume(ConsumeContext<GetAttributeTypeById> context)
    {
        var typeId = new AttributeTypeId(context.Message.Id);

        // Niente FindAsync, usiamo Select per proiettare direttamente in SQL i dati che ci servono
        var dto = await db.AttributeTypes
            .AsNoTracking()
            .Where(a => a.Id == typeId)
            .Select(type => new AttributeTypeDto(
                type.Id.Value,
                type.Name,
                type.Description,
                type.Attributes.Select(a => new AttributeDto(a.Id.Value, a.Name, a.Value)).ToList()
            ))
            .FirstOrDefaultAsync(context.CancellationToken);

        await context.RespondAsync(new GetAttributeTypeByIdResult(dto));
    }
}

public class GetAttributeTypesConsumer(ProductContext db) : IConsumer<GetAttributeTypes>
{
    public async Task Consume(ConsumeContext<GetAttributeTypes> context)
    {
        // Addio query multiple e dizionari in memoria. Una singola query ottimizzata.
        var dtos = await db.AttributeTypes
            .AsNoTracking()
            .OrderBy(a => a.Name)
            .Select(type => new AttributeTypeDto(
                type.Id.Value,
                type.Name,
                type.Description,
                type.Attributes
                    .OrderBy(a => a.Name)
                    .Select(a => new AttributeDto(a.Id.Value, a.Name, a.Value))
                    .ToList()
            ))
            .ToListAsync(context.CancellationToken);

        await context.RespondAsync(new GetAttributeTypesResult(dtos));
    }
}