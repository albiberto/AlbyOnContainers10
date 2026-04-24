namespace ProductInformationManager.Domain.ValueObjects;

public record DescriptionValueId(Guid Value)
{
    public static DescriptionValueId New => new(Guid.NewGuid());
}