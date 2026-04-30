using MessagePack;

namespace AlbyOnContainers.Plugins.DistributedLocks.Model;

/// <summary>
/// Wire payload broadcast on the Redis pub/sub channel for distributed lock events.
/// Uses index-based MessagePack keys for a compact array-shaped binary format
/// (smaller and faster than the property-name map encoding, and stable across deploys).
/// </summary>
[MessagePackObject]
public sealed record LockEventPayload(
    [property: Key(0)] string EntityType,
    [property: Key(1)] string EntityId,
    [property: Key(2)] string? Username,
    [property: Key(3)] bool IsLocked);
