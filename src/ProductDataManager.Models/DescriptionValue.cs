namespace ProductDataManager.Models;

public class DescriptionValue(string value, Guid descriptionTypeId) : AuditableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Value { get; set; } = value;
    public Guid DescriptionTypeId { get; set; } = descriptionTypeId;

    public DescriptionType DescriptionType { get; set; } = null!;
}
