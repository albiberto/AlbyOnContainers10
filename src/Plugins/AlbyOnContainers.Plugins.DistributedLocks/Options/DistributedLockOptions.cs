using System.ComponentModel.DataAnnotations;

namespace AlbyOnContainers.Plugins.DistributedLocks.Options;

using Kernel.Options;

public sealed record DistributedLockOptions : KernelOptions<DistributedLockOptions>
{
    public string ConnectionStringName { get; set; } = "cache";

    [MinLength(3, ErrorMessage = "The Redis channel name must be at least 3 characters long.")]
    public string? RedisChannel { get; set; }

    [Range(typeof(TimeSpan), "00:00:00", "01:00:00", ErrorMessage = "AcquireTimeout must be between 0 seconds and 1 hour.")]
    public TimeSpan AcquireTimeout { get; set; } = TimeSpan.Zero;

    [Required(AllowEmptyStrings = false, ErrorMessage = "The Redis KeyPrefix cannot be empty.")]
    public string KeyPrefix { get; set; } = "global-lock:";

    [Range(1, 256, ErrorMessage = "RecoveryMaxDegreeOfParallelism must be strictly between 1 and 256.")]
    public int RecoveryMaxDegreeOfParallelism { get; set; } = 32;

    /// <summary>
    /// Period of the background self-healing reconciliation loop. Must be strictly &gt; 0
    /// because <c>Observable.Interval(TimeSpan.Zero)</c> throws at runtime.
    /// </summary>
    [Required]
    [Range(typeof(TimeSpan), "00:00:01", "1.00:00:00",
        ErrorMessage = "ReconciliationInterval must be between 1 second and 24 hours.")]
    public TimeSpan ReconciliationInterval { get; set; } = TimeSpan.FromMinutes(5);
}