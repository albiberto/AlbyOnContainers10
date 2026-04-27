using AlbyOnContainers.Kernel.Options;

namespace AlbyOnContainers.Kernel.Persistence.Options;

public sealed class PersistenceOptions : KernelOptions<PersistenceOptions>
{
    public bool EnableSensitiveDataLogging { get; set; } = false;
    public bool EnableDetailedErrors { get; set; } = false;
}