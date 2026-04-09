using AlbyOnContainers.Shared.Domain;
using ProductInformationManager.Domain.ValueObjects;

namespace ProductInformationManager.Domain;

public class ProductAttribute : AuditableEntity
{
    public ProductId ProductId { get; init; } = null!;
    public AttributeId AttributeId { get; init; } = null!;
    
    // Proprietà di navigazione per EF Core
    public Product Product { get; private set; } = null!;
    public Attribute Attribute { get; private set; } = null!;
}