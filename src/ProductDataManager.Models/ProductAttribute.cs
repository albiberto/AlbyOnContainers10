namespace ProductDataManager.Models;

public class ProductAttribute(Guid productId, Guid attributeId) : AuditableEntity
{
    public Guid ProductId { get; set; } = productId;
    public Guid AttributeId { get; set; } = attributeId;

    public Product Product { get; set; } = null!;
    public Attribute Attribute { get; set; } = null!;
}
