namespace AlbyOnContainers.Kernel.Domain.SeedWork;

/// <summary>
/// Base class for all Aggregate Roots. Extends AuditableEntity with
/// domain event collection management. Events appended by behavior methods
/// are dispatched automatically by the persistence interceptor and cleared
/// after SaveChangesAsync — consumers must never call IPublishEndpoint directly.
/// </summary>
public abstract class AggregateRoot : AuditableEntity
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void AppendEvent(IDomainEvent domainEvent)
        => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents()
        => _domainEvents.Clear();
}
