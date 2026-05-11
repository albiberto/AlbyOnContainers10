using System.ComponentModel.DataAnnotations;
using AlbyOnContainers.Kernel.Options;

namespace AlbyOnContainers.Kernel.Observability.Options;

public sealed record ObservabilityOptions : KernelOptions<ObservabilityOptions>, IValidatableObject
{
    public bool EnableHttpClientTracing { get; set; } = true;
    public bool EnableEntityFrameworkTracing { get; set; } = true;
    public bool EnableOtlpExporter { get; set; } = false;
    public bool EnableMassTransitTracing { get; set; } = true;

    /// <summary>
    ///     Probability (0.0 – 1.0) used by the parent-based ratio sampler to decide whether a new
    ///     trace is recorded. Defaults to 1.0 (100%) for development; in production you typically
    ///     drop this to 0.05–0.10 via <c>appsettings.Production.json</c> or the
    ///     <c>Observability__TraceSamplingProbability</c> environment variable.
    /// </summary>
    [Range(0.0, 1.0, ErrorMessage = "TraceSamplingProbability must be between 0.0 and 1.0.")]
    public double TraceSamplingProbability { get; set; } = 1.0;

    public string[] CustomMeters { get; set; } = [];
    public string[] CustomTracingSources { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        foreach (var error in ValidateNameArray(CustomMeters, nameof(CustomMeters)))
            yield return error;

        foreach (var error in ValidateNameArray(CustomTracingSources, nameof(CustomTracingSources)))
            yield return error;
    }

    private static IEnumerable<ValidationResult> ValidateNameArray(string[] values, string memberName)
    {
        if (values.Any(string.IsNullOrWhiteSpace))
            yield return new ValidationResult(
                $"{memberName} must not contain empty or whitespace entries.",
                [memberName]);

        var duplicates = values
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .GroupBy(v => v, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToArray();

        if (duplicates.Length > 0)
            yield return new ValidationResult(
                $"{memberName} contains duplicate entries: {string.Join(", ", duplicates)}.",
                [memberName]);
    }
}
