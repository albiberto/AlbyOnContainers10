using System.ComponentModel.DataAnnotations;
using AlbyOnContainers.Kernel.Options;

namespace AlbyOnContainers.Kernel.Observability.Options;

public sealed class ObservabilityOptions : KernelOptions<ObservabilityOptions>
{
    [Required(AllowEmptyStrings = false)]
    public string ServiceName { get; set; } = "AlbyOnContainers.UnknownService";

    [Required(AllowEmptyStrings = false)]
    public string Namespace { get; set; } = "AlbyOnContainers";

    public bool EnableHttpClientTracing { get; set; } = true;
    public bool EnableEntityFrameworkTracing { get; set; } = true;
    public bool EnableOtlpExporter { get; set; } = false;

    public string[] CustomMeters { get; set; } = [];
    public string[] CustomTracingSources { get; set; } = [];
}