using System.ComponentModel.DataAnnotations;

namespace AlbyOnContainers.Kernel.Caching.Options;

public class CachingOptions
{
    public const string SectionName = "Caching";

    [Required]
    public string RedisConnectionString { get; set; } = string.Empty;

    public int DurationInMinutes { get; set; } = 30;
    public bool IsFailSafeEnabled { get; set; } = true;
    public int FailSafeMaxDurationInHours { get; set; } = 2;
    public int JitterMaxDurationInSeconds { get; set; } = 2;
}
