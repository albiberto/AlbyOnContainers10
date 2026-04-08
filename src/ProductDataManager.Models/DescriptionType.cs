namespace ProductDataManager.Models;

public class DescriptionType(string name, string? description = null) : AuditableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = name;
    public string? Description { get; set; } = description;

    public ICollection<DescriptionValue> Values { get; set; } = [];
    public ICollection<DescriptionTypeCategory> DescriptionTypeCategories { get; set; } = [];
}
