namespace ProductDataManager.Models.ValueObjects;

public record DescriptionValueId(Guid Value)
{
    public static DescriptionValueId New => new(Guid.NewGuid());
}