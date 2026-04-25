using System.ComponentModel.DataAnnotations;

namespace AlbyOnContainers.Kernel.Caching.Options;

public class CachingOptions
{
    public const string SectionName = "Caching";

    [Required]
    public string RedisConnectionString { get; set; } = string.Empty;
}
