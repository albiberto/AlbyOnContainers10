namespace AlbyOnContainers.Kernel.Resilience.Options;

using System.ComponentModel.DataAnnotations;
using Kernel.Options;

public sealed record ResilienceOptions : KernelOptions<ResilienceOptions>, IValidatableObject
{
    [Range(1, 10, ErrorMessage = "Max retries must be between 1 and 10.")]
    public int MaxRetryAttempts { get; set; } = 3;

    [Range(typeof(TimeSpan), "00:00:01", "00:00:10", ErrorMessage = "Initial delay must be between 1 and 10 seconds.")]
    public TimeSpan Delay { get; set; } = TimeSpan.FromSeconds(2);

    [Range(typeof(TimeSpan), "00:00:05", "00:05:00", ErrorMessage = "Timeout must be configured.")]
    public TimeSpan OverallTimeout { get; set; } = TimeSpan.FromSeconds(30);

    public bool UseExponentialBackoff { get; set; } = true;

    public CircuitBreakerOptions? CircuitBreaker { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (CircuitBreaker is null) yield break;

        var nestedContext = new ValidationContext(CircuitBreaker);
        var nestedResults = new List<ValidationResult>();

        if (Validator.TryValidateObject(CircuitBreaker, nestedContext, nestedResults, validateAllProperties: true)) yield break;

        foreach (var result in nestedResults)
            yield return new ValidationResult(
                result.ErrorMessage,
                result.MemberNames.Select(member => $"{nameof(CircuitBreaker)}.{member}"));
    }
}
