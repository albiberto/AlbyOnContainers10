namespace ProductInformationManager.Domain.ValueObjects;

public record AttributeTypeId(Guid Value)
{
    public static AttributeTypeId New => new(Guid.NewGuid());
}