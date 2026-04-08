namespace ProductDataManager.Models.ValueObjects;

public record AttributeId(Guid Value)
{
    public static AttributeId New => new(Guid.NewGuid());
}