namespace AlbyOnContainers.Kernel.Observability.Options;

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using AlbyOnContainers.Kernel.Options;

public class ObservabilityOptions : KernelOptions<ObservabilityOptions>
{
    [Required]
    public string ServiceName { get; set; } = string.Empty;

    [Required]
    public string Namespace { get; set; } = string.Empty;

    public string? OtlpEndpoint { get; set; }

    // Sorgenti standard incluse automaticamente dalla piattaforma
    public List<string> DefaultTracingSources { get; set; } = ["MassTransit"];
    
    // Sorgenti specifiche fornite dalla singola microservice
    public List<string> CustomTracingSources { get; set; } = [];

    // --- External Instrumentations (Bytecode/DiagnosticSource based) ---
    public bool EnableEntityFrameworkTracing { get; set; } = true;
    public bool EnableHttpClientTracing { get; set; } = true;

    // --- Metrics ---
    public List<string> CustomMeters { get; set; } = ["MassTransit", "Microsoft.EntityFrameworkCore"];

    public bool EnableOtlpExporter { get; set; } = true;
}