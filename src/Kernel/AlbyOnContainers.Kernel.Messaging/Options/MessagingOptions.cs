using System.ComponentModel.DataAnnotations;
using AlbyOnContainers.Kernel.Options;

namespace AlbyOnContainers.Kernel.Messaging.Options;

public sealed record MessagingOptions : KernelOptions<MessagingOptions>, IValidatableObject
{
    public string? ConnectionStringName { get; set; }

    public string? ConnectionString { get; set; }

    public string? Host { get; set; }

    /// <summary>
    /// AMQP port. Defaults to 5672 (or 5671 with TLS). Aspire injects a dynamic port,
    /// so the bootstrap layer is expected to populate this from the connection string.
    /// </summary>
    [Range(1, 65535)]
    public int Port { get; set; } = 5672;

    public string? Username { get; set; }
    public string? Password { get; set; }

    /// <summary>
    /// Enable TLS on the RabbitMQ connection (AMQPS).
    /// </summary>
    public bool UseSsl { get; set; } = false;

    /// <summary>
    /// Number of retries for transient failures on the bus. Set to 0 to disable retries entirely.
    /// </summary>
    [Range(0, 100)]
    public int RetryCount { get; set; } = 3;

    [Range(typeof(TimeSpan), "00:00:00.100", "00:05:00", ErrorMessage = "RetryInitialInterval must be between 100ms and 5 minutes.")]
    public TimeSpan RetryInitialInterval { get; set; } = TimeSpan.FromSeconds(2);

    [Range(typeof(TimeSpan), "00:00:01", "01:00:00", ErrorMessage = "RetryMaxInterval must be between 1 second and 1 hour.")]
    public TimeSpan RetryMaxInterval { get; set; } = TimeSpan.FromSeconds(30);

    [Range(typeof(TimeSpan), "00:00:00.100", "01:00:00", ErrorMessage = "RetryDeltaInterval must be between 100ms and 1 hour.")]
    public TimeSpan RetryDeltaInterval { get; set; } = TimeSpan.FromSeconds(5);

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(ConnectionString) && string.IsNullOrWhiteSpace(ConnectionStringName))
        {
            if (string.IsNullOrWhiteSpace(Host))
                yield return new ValidationResult(
                    "Host is required when neither ConnectionString nor ConnectionStringName is configured.",
                    [nameof(Host)]);

            if (string.IsNullOrWhiteSpace(Username))
                yield return new ValidationResult(
                    "Username is required when neither ConnectionString nor ConnectionStringName is configured.",
                    [nameof(Username)]);

            if (string.IsNullOrWhiteSpace(Password))
                yield return new ValidationResult(
                    "Password is required when neither ConnectionString nor ConnectionStringName is configured.",
                    [nameof(Password)]);
        }

        // MassTransit's exponential backoff requires Initial <= Max; otherwise it throws at runtime.
        if (RetryCount > 0 && RetryInitialInterval > RetryMaxInterval)
            yield return new ValidationResult(
                $"RetryInitialInterval ({RetryInitialInterval}) cannot be greater than RetryMaxInterval ({RetryMaxInterval}).",
                [nameof(RetryInitialInterval), nameof(RetryMaxInterval)]);

        // Delta is the per-attempt growth step; growing past the gap between Initial and Max
        // means the schedule will saturate immediately at Max — most likely a misconfiguration.
        if (RetryCount > 0 && RetryDeltaInterval > RetryMaxInterval - RetryInitialInterval)
            yield return new ValidationResult(
                $"RetryDeltaInterval ({RetryDeltaInterval}) cannot exceed the gap between RetryInitialInterval and RetryMaxInterval ({RetryMaxInterval - RetryInitialInterval}).",
                [nameof(RetryDeltaInterval)]);
    }
}
