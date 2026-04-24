namespace ProductInformationManager.Infrastructure.Options;

public sealed class EfCoreObservabilityOptions
{
    public const string SectionName = "Observability:EfCore";

    public double SlowCommandThresholdMs { get; set; } = 500;
}
