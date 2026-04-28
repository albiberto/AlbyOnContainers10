using MessagePack;

namespace AlbyOnContainers.Plugins.DistributedLocks.Model;

[MessagePackObject]
public sealed record LockEventPayload(
    [property: Key(0)] string EntityType,
    [property: Key(1)] string EntityId,
    [property: Key(2)] string? Username,
    [property: Key(3)] bool IsLocked
);