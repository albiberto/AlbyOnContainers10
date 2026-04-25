using ProductInformationManager.Domain.ValueObjects;

namespace ProductInformationManager.Domain;

public class ProductDescription : AuditableEntity
{
    public ProductId ProductId { get; init; } = null!;
    public DescriptionValueId DescriptionValueId { get; init; } = null!;

    public Product Product { get; private set; } = null!;
    public DescriptionValue DescriptionValue { get; private set; } = null!;
}