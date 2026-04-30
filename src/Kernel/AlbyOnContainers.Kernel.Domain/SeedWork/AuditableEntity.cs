namespace AlbyOnContainers.Kernel.Domain.SeedWork;

public abstract class AuditableEntity
{
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public string? CreatedBy { get; private set; }
    public string? UpdatedBy { get; private set; }

    /// <summary>
    /// Sets creation audit metadata. Intentionally <c>internal</c>: must only be invoked
    /// by the persistence-layer interceptor (AuditableEntityInterceptor), never by domain
    /// code or callers outside the kernel.
    /// </summary>
    internal void SetCreationInfo(string createdBy)
    {
        CreatedBy = createdBy;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Sets update audit metadata. Same encapsulation contract as <see cref="SetCreationInfo"/>.
    /// </summary>
    internal void SetUpdateInfo(string updatedBy)
    {
        UpdatedBy = updatedBy;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}