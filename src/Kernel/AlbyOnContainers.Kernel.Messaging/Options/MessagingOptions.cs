using System.ComponentModel.DataAnnotations;
using AlbyOnContainers.Kernel.Options;

namespace AlbyOnContainers.Kernel.Messaging.Options;

public sealed class MessagingOptions : KernelOptions<MessagingOptions>
{
    [Required] public string Host { get; set; } = "localhost";
    [Required] public string Username { get; set; } = "guest";
    [Required] public string Password { get; set; } = "guest";

    public int RetryCount { get; set; } = 3;
    public int RetryInitialIntervalSeconds { get; set; } = 2;
    public int RetryMaxIntervalSeconds { get; set; } = 30;
}