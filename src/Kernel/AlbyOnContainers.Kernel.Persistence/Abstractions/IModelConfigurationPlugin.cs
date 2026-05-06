using Microsoft.EntityFrameworkCore;

namespace AlbyOnContainers.Kernel.Persistence.Abstractions;

/// <summary>
/// Interface that allows external modules (e.g., Messaging) to inject
/// tables and configurations into the DbContext in a completely transparent way.
/// </summary>
public interface IModelConfigurationPlugin
{
    void Apply(ModelBuilder modelBuilder);
}