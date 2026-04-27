using AlbyOnContainers.Kernel.Options;

namespace AlbyOnContainers.Kernel.Persistence.Options;

public sealed class PersistenceOptions : KernelOptions<PersistenceOptions>
{
    public bool EnableSensitiveDataLogging { get; set; } = false;
    public bool EnableDetailedErrors { get; set; } = false;
    
    public int MaxRetryCount { get; set; } = 3;
    public int CommandTimeout { get; set; } = 30;
}