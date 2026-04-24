using ProductInformationManager.Messages;

namespace ProductInformationManager.Web.Notifiers;

public abstract record CategoryChangeNotification;

public record CategoryCreated(Guid Id, string Name, string? Description, string Path, Guid? ParentId) : CategoryChangeNotification
{
    public CategoryDto ToDto() => new(Id, Name, Description, Path, ParentId, false);
}

public record CategoryUpdated(Guid Id, string Name, string? Description, string Path, Guid? ParentId) : CategoryChangeNotification
{
    public CategoryDto ToDto() => new(Id, Name, Description, Path, ParentId, false);
}

public record CategoryDeleted(Guid Id) : CategoryChangeNotification;