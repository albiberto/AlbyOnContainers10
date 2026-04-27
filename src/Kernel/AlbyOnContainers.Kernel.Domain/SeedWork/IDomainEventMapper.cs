namespace AlbyOnContainers.Kernel.Domain.SeedWork;

/// <summary>
/// Translates a Domain Event (bounded-context internal) to its corresponding
/// Integration Event (cross-service contract) before publishing to the bus.
/// Implementations are registered in each microservice's Application layer,
/// keeping the Kernel fully agnostic of any specific event contracts.
/// </summary>
public interface IDomainEventMapper
{
    /// <summary>
    /// Returns the integration event(s) to publish for the given domain event,
    /// or an empty enumerable if the event should not cross service boundaries.
    /// </summary>
    IEnumerable<object> Map(IDomainEvent domainEvent);
}
