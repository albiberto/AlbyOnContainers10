using System.ComponentModel.DataAnnotations;
using AlbyOnContainers.Kernel.Options;

namespace AlbyOnContainers.Kernel.Messaging.Options;

public sealed class MessagingOptions : KernelOptions<MessagingOptions>
{
    [Required] public string Host { get; set; } = null!;
    [Required] public string Username { get; set; } = null!;
    [Required] public string Password { get; set; } = null!;

    public int RetryCount { get; set; } = 3;
    
    public TimeSpan RetryInitialInterval { get; set; } = TimeSpan.FromSeconds(2);
    public TimeSpan RetryMaxInterval { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan RetryDeltaInterval { get; set; } = TimeSpan.FromSeconds(5);
}