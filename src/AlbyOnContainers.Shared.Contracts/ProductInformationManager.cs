namespace AlbyOnContainers.Shared.Contracts;

public record CategoryCreatedEvent(Guid Id, string Name, string Description, string Path, Guid? ParentId, long Version = 0) : ContractBase(Version);

public record CategoryUpdatedEvent(Guid Id, string Name, string Description, string Path, Guid? ParentId, long Version = 0) : ContractBase(Version);
public record CategoryDeletedEvent(Guid Id) : ContractBase;