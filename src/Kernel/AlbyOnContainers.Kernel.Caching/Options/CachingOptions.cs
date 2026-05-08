using System.ComponentModel.DataAnnotations;
using AlbyOnContainers.Kernel.Options;

namespace AlbyOnContainers.Kernel.Caching.Options;

public record CachingOptions: KernelOptions<CachingOptions>
{
    public TimeSpan Duration { get; set; } = TimeSpan.FromMinutes(30);

    public TimeSpan FailSafeMaxDuration { get; set; } = TimeSpan.FromHours(2);

    public TimeSpan JitterMaxDuration { get; set; } = TimeSpan.FromSeconds(2);

    public bool IsFailSafeEnabled { get; set; } = true;
}