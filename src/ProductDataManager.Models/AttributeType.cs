namespace ProductDataManager.Models;

public class AttributeType(string name, string? description = null) : AuditableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = name;
    public string? Description { get; set; } = description;

    public ICollection<ProductAttribute> ProductAttributes { get; set; } = [];
}
