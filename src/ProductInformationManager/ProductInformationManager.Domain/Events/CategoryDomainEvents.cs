namespace ProductInformationManager.Domain.Events;

public sealed record CategoryCreatedDomainEvent(
    Guid Id,
    string Name,
    string Description,
    string Path,
    Guid? ParentId) : IDomainEvent;

public sealed record CategoryUpdatedDomainEvent(
    Guid Id,
    string Name,
    string Description,
    string Path,
    Guid? ParentId) : IDomainEvent;

public sealed record CategoryDeletedDomainEvent(Guid Id) : IDomainEvent;
