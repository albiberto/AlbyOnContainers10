using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AlbyOnContainers.Kernel.Observability.Options;

public class ObservabilityOptions
{
    public const string SectionName = "Observability";

    [Required]
    public string ServiceName { get; set; } = string.Empty;

    [Required]
    public string Namespace { get; set; } = string.Empty;

    [Required]
    public string Environment { get; set; } = "Development";

    public string? OtlpEndpoint { get; set; }

    public List<string> CustomMeters { get; set; } = ["MassTransit", "Microsoft.EntityFrameworkCore"];
    public List<string> CustomTracingSources { get; set; } = ["MassTransit"];
    public bool EnableOtlpExporter { get; set; } = true;
}
