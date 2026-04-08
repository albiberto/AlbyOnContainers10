namespace ProductDataManager.Models.ValueObjects;

public record CategoryId(Guid Value)
{
    public static CategoryId New => new(Guid.NewGuid());
}