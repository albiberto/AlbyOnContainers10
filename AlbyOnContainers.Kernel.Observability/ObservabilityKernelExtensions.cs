using AlbyOnContainers.Kernel.Abstraction;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace AlbyOnContainers.Kernel.Observability;

public static class ObservabilityKernelExtensions
{
    public static IKernelBuilder WithObservability(this IKernelBuilder builder)
    {
        builder.ConfigureOpenTelemetry();
        builder.AddDefaultHealthChecks();
        
        builder.Host.Services.AddServiceDiscovery();
        builder.Host.Services.ConfigureHttpClientDefaults(http =>
        {
            http.AddStandardResilienceHandler();
            http.AddServiceDiscovery();
        });

        return builder;
    }

    private static IKernelBuilder ConfigureOpenTelemetry(this IKernelBuilder builder)
    {
        builder.Host.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
            logging.ParseStateValues = true;
        });

        builder.Host.Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    // I nomi dei meter possono essere iniettati o configurati in futuro se necessario
                    .AddMeter(
                        "MassTransit",
                        "Microsoft.EntityFrameworkCore",
                        "ProductInformationManager.Application",
                        "ProductInformationManager.Infrastructure");
            })
            .WithTracing(tracing =>
            {
                tracing.AddSource(builder.Host.Environment.ApplicationName)
                    .AddSource("MassTransit")
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation();
            });

        var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Host.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);
        if (useOtlpExporter)
        {
            builder.Host.Services.AddOpenTelemetry().UseOtlpExporter();
        }

        return builder;
    }

    private static IKernelBuilder AddDefaultHealthChecks(this IKernelBuilder builder)
    {
        builder.Host.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        return builder;
    }

    // Metodo di estensione per mappare gli endpoint nell'app (es. /health)
    public static WebApplication MapKernelObservabilityEndpoints(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.MapHealthChecks("/health");
            app.MapHealthChecks("/alive", new HealthCheckOptions
            {
                Predicate = r => r.Tags.Contains("live")
            });
        }
        return app;
    }
}