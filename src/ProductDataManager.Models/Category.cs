namespace ProductDataManager.Models;

public class Category(string name, string? description = null, Guid? parentId = null) : AuditableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = name;
    public string? Description { get; set; } = description;
    public string Path { get; set; } = string.Empty; // ltree path
    public Guid? ParentId { get; set; } = parentId;

    public Category? Parent { get; set; }
    public ICollection<Category> Children { get; set; } = [];
    public ICollection<DescriptionTypeCategory> DescriptionTypeCategories { get; set; } = [];
    public ICollection<Product> Products { get; set; } = [];
}
