namespace ProductInformationManager.Contracts;

public sealed record CategoryCreatedEvent(
    Guid Id,
    string Name,
    string Description,
    string Path,
    Guid? ParentId,
    long Version = 0);
