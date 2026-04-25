namespace AlbyOnContainers.Kernel.Domain.SeedWork;

/// <summary>
/// Marker interface for all Domain Events raised by Aggregate Roots.
/// Events are dispatched automatically by the DomainEventDispatcherInterceptor
/// at the end of a Unit of Work, without any manual publish in consumers.
/// </summary>
public interface IDomainEvent;
