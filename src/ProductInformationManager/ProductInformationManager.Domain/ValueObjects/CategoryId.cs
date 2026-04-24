namespace ProductInformationManager.Domain.ValueObjects;

public record CategoryId(Guid Value)
{
    public static CategoryId New => new(Guid.NewGuid());
}