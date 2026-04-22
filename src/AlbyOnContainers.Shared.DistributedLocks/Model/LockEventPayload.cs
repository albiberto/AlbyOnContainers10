namespace AlbyOnContainers.Shared.DistributedLocks.Model;

public record LockEventPayload(string EntityType, string EntityId, string UserId, bool IsLocked);