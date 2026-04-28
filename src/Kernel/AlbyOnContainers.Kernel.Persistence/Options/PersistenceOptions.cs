namespace AlbyOnContainers.Kernel.Persistence.Options;

using System.ComponentModel.DataAnnotations;
using Kernel.Options;

public sealed class PersistenceOptions : KernelOptions<PersistenceOptions>
{
    public bool EnableSensitiveDataLogging { get; set; } = false;
    public bool EnableDetailedErrors { get; set; } = false;

    [Range(100, 10000)]
    public int SlowQueryThresholdMs { get; set; } = 500;

    public string MetricPrefix { get; set; } = string.Empty;
    
    public bool RunMigrationsOnStartup { get; set; } = true;
}