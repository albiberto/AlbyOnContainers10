using System.ComponentModel.DataAnnotations;
using AlbyOnContainers.Kernel.Options;

namespace AlbyOnContainers.Kernel.Messaging.Options;

public sealed record MessagingOptions : KernelOptions<MessagingOptions>
{
    [Required] public string Host { get; set; } = null!;

    /// <summary>
    /// AMQP port. Defaults to 5672 (or 5671 with TLS). Aspire injects a dynamic port,
    /// so the bootstrap layer is expected to populate this from the connection string.
    /// </summary>
    [Range(1, 65535)]
    public int Port { get; set; } = 5672;

    [Required] public string Username { get; set; } = null!;
    [Required] public string Password { get; set; } = null!;

    /// <summary>
    /// Enable TLS on the RabbitMQ connection (AMQPS).
    /// </summary>
    public bool UseSsl { get; set; } = false;

    /// <summary>
    /// Number of retries for transient failures on the bus. Set to 0 to disable retries entirely.
    /// </summary>
    [Range(0, 100)]
    public int RetryCount { get; set; } = 3;

    public TimeSpan RetryInitialInterval { get; set; } = TimeSpan.FromSeconds(2);
    public TimeSpan RetryMaxInterval { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan RetryDeltaInterval { get; set; } = TimeSpan.FromSeconds(5);
}