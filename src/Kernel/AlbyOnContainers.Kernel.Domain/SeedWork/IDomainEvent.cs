namespace AlbyOnContainers.Kernel.Domain.SeedWork;

/// <summary>
/// Marker interface for all Domain Events raised by Aggregate Roots.
/// Events are dispatched automatically by the DomainEventDispatcherInterceptor
/// at the end of a Unit of Work, without any manual publish in consumers.
/// </summary>
/// <remarks>
/// Implementers SHOULD provide an immutable record with at minimum:
/// <list type="bullet">
///   <item><description><c>EventId</c> — unique identifier for idempotency / tracing.</description></item>
///   <item><description><c>OccurredOn</c> — wall-clock timestamp of the event.</description></item>
/// </list>
/// </remarks>
public interface IDomainEvent
{
    /// <summary>
    /// Unique identifier of the domain event. Used for tracing and idempotent processing.
    /// </summary>
    Guid EventId => Guid.NewGuid();

    /// <summary>
    /// Wall-clock timestamp at which the event was raised by the aggregate.
    /// </summary>
    DateTimeOffset OccurredOn => DateTimeOffset.UtcNow;
}
