namespace AlbyOnContainers.Shared.DistributedLocks.Model;

public abstract record Emit(string EntityType, string EntityId)
{
    public record Locked(string EntityType, string EntityId, string UserId) : Emit(EntityType, EntityId);
    public record Unlocked(string EntityType, string EntityId) : Emit(EntityType, EntityId);
}