using AlbyOnContainers.Kernel.Domain.SeedWork;
using ProductInformationManager.Contracts;
using ProductInformationManager.Domain.Events;

namespace ProductInformationManager.Application.Mapping;

/// <summary>
/// Translates PIM-specific domain events to their public integration event contracts
/// (defined in ProductInformationManager.Contracts). This keeps the Domain layer
/// free of any dependency on the Contracts / messaging infrastructure.
/// </summary>
public sealed class PimDomainEventMapper : IDomainEventMapper
{
    public IEnumerable<object> Map(IDomainEvent domainEvent) => domainEvent switch
    {
        CategoryCreatedDomainEvent e =>
        [
            new CategoryCreatedEvent(e.Id, e.Name, e.Description, e.Path, e.ParentId)
        ],

        CategoryUpdatedDomainEvent e =>
        [
            new CategoryUpdatedEvent(e.Id, e.Name, e.Description, e.Path, e.ParentId)
        ],

        CategoryDeletedDomainEvent e =>
        [
            new CategoryDeletedEvent(e.Id)
        ],

        // Unknown / internal-only events: produce no integration events
        _ => []
    };
}
