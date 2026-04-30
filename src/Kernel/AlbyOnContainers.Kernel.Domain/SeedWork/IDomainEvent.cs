namespace AlbyOnContainers.Kernel.Domain.SeedWork;

/// <summary>
/// Marker interface for all Domain Events raised by Aggregate Roots.
/// Events are dispatched automatically by the DomainEventDispatcherInterceptor
/// at the end of a Unit of Work, without any manual publish in consumers.
/// </summary>
/// <remarks>
/// IMPLEMENTATION CONTRACT (convention, not enforced by the type system):
/// implementers SHOULD be immutable records exposing the following metadata
/// so that downstream pipelines (tracing, idempotency, outbox) can rely on it:
/// <list type="bullet">
///   <item><description><c>Guid EventId</c> — unique id, captured ONCE in the record initializer
///   (e.g. <c>public Guid EventId { get; init; } = Guid.NewGuid();</c>).</description></item>
///   <item><description><c>DateTimeOffset OccurredOn</c> — wall-clock timestamp, captured ONCE in the record initializer.</description></item>
/// </list>
/// <para>
/// These members are intentionally NOT declared on the interface as default-implemented properties:
/// default interface members would re-evaluate the expression on every getter call, breaking event identity.
/// </para>
/// </remarks>
public interface IDomainEvent;
