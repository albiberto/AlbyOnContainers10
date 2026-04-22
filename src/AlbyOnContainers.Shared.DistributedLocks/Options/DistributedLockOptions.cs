using System.ComponentModel.DataAnnotations;

namespace AlbyOnContainers.Shared.DistributedLocks.Options;

public class DistributedLockOptions
{
    [Required(ErrorMessage = "The Redis Pub/Sub channel is required.")]
    [MinLength(3, ErrorMessage = "The Redis channel name must be at least 3 characters long.")]
    public string? RedisChannel { get; set; }

    [Range(typeof(TimeSpan), "00:00:00", "01:00:00", ErrorMessage = "AcquireTimeout must be between 0 seconds and 1 hour.")]
    public TimeSpan AcquireTimeout { get; set; } = TimeSpan.Zero;
    
    [Required(AllowEmptyStrings = false, ErrorMessage = "The Redis KeyPrefix cannot be empty.")]
    public string KeyPrefix { get; set; } = "global-lock:";
}