namespace ProductDataManager.Models;

public class DescriptionTypeCategory(Guid descriptionTypeId, Guid categoryId) : AuditableEntity
{
    public Guid DescriptionTypeId { get; set; } = descriptionTypeId;
    public Guid CategoryId { get; set; } = categoryId;

    public DescriptionType DescriptionType { get; set; } = null!;
    public Category Category { get; set; } = null!;
}
