namespace ProductDataManager.Models.Abstract;

public abstract class AuditableEntity
{
    public DateTime CreatedAt { get; internal set; }
    public DateTime? UpdatedAt { get; internal set; }
    public string? CreatedBy { get; internal set; }
    public string? UpdatedBy { get; internal set; }
}