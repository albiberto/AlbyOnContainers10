using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AlbyOnContainers.Kernel.Observability.Options;

public class ObservabilityOptions
{
    public const string SectionName = "Observability";

    [Required]
    public List<string> MeterNames { get; set; } = ["MassTransit", "Microsoft.EntityFrameworkCore"];

    [Required]
    public List<string> TraceSources { get; set; } = ["MassTransit"];

    public bool EnableOtlpExporter { get; set; } = true;
}
