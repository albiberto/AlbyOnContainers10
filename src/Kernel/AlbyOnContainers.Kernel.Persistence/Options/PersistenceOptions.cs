namespace AlbyOnContainers.Kernel.Persistence.Options;

using System;
using System.ComponentModel.DataAnnotations;
using Kernel.Options;

public sealed class PersistenceOptions : KernelOptions<PersistenceOptions>
{
    [Required(ErrorMessage = "The metric prefix is required to ensure proper OpenTelemetry/Prometheus namespace grouping.")]
    [RegularExpression(@"^[a-zA-Z_][a-zA-Z0-9_]*$", ErrorMessage = "The metric prefix must contain only alphanumeric characters or underscores, and cannot start with a number.")]
    public string MetricPrefix { get; set; } = string.Empty;
    
    [Range(typeof(TimeSpan), "00:00:05", "00:05:00", ErrorMessage = "The distributed lock timeout must be between 5 seconds and 5 minutes to prevent infinite blocking or premature failures during container startup migrations.")]
    public TimeSpan LockTimeout { get; set; } = TimeSpan.FromSeconds(90);
    
    public bool EnableSensitiveDataLogging { get; set; }

    public bool EnableDetailedErrors { get; set; }

    public bool RunMigrationsOnStartup { get; set; }
}