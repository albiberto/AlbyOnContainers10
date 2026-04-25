using System.ComponentModel.DataAnnotations;

namespace AlbyOnContainers.Kernel.Persistence.Options;

public class PersistenceOptions
{
    public const string SectionName = "Persistence";

    [Required]
    public string ConnectionString { get; set; } = string.Empty;

    public string Provider { get; set; } = "Postgres";

    public bool EnableDetailedErrors { get; set; } = true;
    public bool EnableSensitiveDataLogging { get; set; } = true;

    /// <summary>Queries exceeding this threshold will be logged as warnings.</summary>
    public int SlowCommandThresholdMs { get; set; } = 500;
}
