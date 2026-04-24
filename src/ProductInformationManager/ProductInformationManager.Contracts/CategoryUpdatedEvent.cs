namespace ProductInformationManager.Contracts;

public record CategoryUpdatedEvent(Guid Id, string Name, string Description, string Path, Guid? ParentId, long Version = 0) : ContractBase(Version);