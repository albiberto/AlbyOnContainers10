namespace AlbyOnContainers.Kernel.Messaging.Contracts;

/// <summary>
/// Base contract for all integration messages exchanged on the bus.
/// Provides correlation metadata used by the kernel telemetry pipeline.
/// </summary>
public abstract record ContractBase
{
    /// <summary>
    /// Unique identifier of the message. Defaults to a new GUID v4 unless explicitly set.
    /// </summary>
    public Guid MessageId { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Wall-clock timestamp at which the originating event occurred. Defaults to <c>UtcNow</c>.
    /// </summary>
    public DateTimeOffset OccurredOn { get; init; } = DateTimeOffset.UtcNow;
}