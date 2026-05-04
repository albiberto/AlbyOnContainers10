namespace AlbyOnContainers.Kernel.Persistence.Options;

using System.ComponentModel.DataAnnotations;
using Kernel.Options;

public sealed class PersistenceOptions : KernelOptions<PersistenceOptions>
{
    [Required(ErrorMessage = "The database connection string is strictly required."), MinLength(10, ErrorMessage = "The connection string must be at least 10 characters long.")]
    public string ConnectionString { get; set; } = string.Empty;

    [Required(ErrorMessage = "The metric prefix is required to ensure proper OpenTelemetry/Prometheus namespace grouping."), RegularExpression(@"^[a-zA-Z_][a-zA-Z0-9_]*$", ErrorMessage = "The metric prefix must contain only alphanumeric characters or underscores, and cannot start with a number.")]
    public string MetricPrefix { get; set; } = string.Empty;

    public bool EnableSensitiveDataLogging { get; set; } = false;

    public bool EnableDetailedErrors { get; set; } = false;

    public bool RunMigrationsOnStartup { get; set; } = false;
}