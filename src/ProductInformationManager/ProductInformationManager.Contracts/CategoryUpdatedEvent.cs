namespace ProductInformationManager.Contracts;

public sealed record CategoryUpdatedEvent(
    Guid Id,
    string Name,
    string Description,
    string Path,
    Guid? ParentId,
    long Version = 0);
