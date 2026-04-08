namespace ProductDataManager.Models.ValueObjects;

public record AttributeTypeId(Guid Value)
{
    public static AttributeTypeId New => new(Guid.NewGuid());
}