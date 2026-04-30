using MessagePack;

namespace AlbyOnContainers.Plugins.DistributedLocks.Model;

[MessagePackObject(keyAsPropertyName: true)]
public record LockEventPayload(string EntityType, string EntityId, string? Username, bool IsLocked);
