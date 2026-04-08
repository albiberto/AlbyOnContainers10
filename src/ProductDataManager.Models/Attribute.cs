namespace ProductDataManager.Models;

public class Attribute(string name, string value, Guid attributeTypeId) : AuditableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = name;
    public string Value { get; set; } = value;
    public Guid AttributeTypeId { get; set; } = attributeTypeId;

    public AttributeType AttributeType { get; set; } = null!;
    public ICollection<ProductAttribute> ProductAttributes { get; set; } = [];
}
