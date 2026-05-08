namespace AlbyOnContainers.Kernel.Resilience.Options;

using System.ComponentModel.DataAnnotations;

public sealed record CircuitBreakerOptions
{
    [Range(0.1, 1.0, ErrorMessage = "Failure ratio must be between 0.1 (10%) and 1.0 (100%).")]
    public double FailureRatio { get; set; } = 0.5;

    [Range(2, 1000, ErrorMessage = "Minimum throughput must be between 2 and 1000 calls.")]
    public int MinimumThroughput { get; set; } = 10;

    [Range(typeof(TimeSpan), "00:00:01", "00:10:00", ErrorMessage = "Break duration must be between 1 second and 10 minutes.")]
    public TimeSpan BreakDuration { get; set; } = TimeSpan.FromSeconds(30);

    [Range(typeof(TimeSpan), "00:00:05", "00:10:00", ErrorMessage = "Sampling duration must be between 5 seconds and 10 minutes.")]
    public TimeSpan SamplingDuration { get; set; } = TimeSpan.FromSeconds(60);
}
