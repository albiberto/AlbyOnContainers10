namespace ProductDataManager.Models.ValueObjects;

public record DescriptionTypeId(Guid Value)
{
    public static DescriptionTypeId New => new(Guid.NewGuid());
}