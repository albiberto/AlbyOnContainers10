using System;
using AlbyOnContainers.Kernel.Abstraction;
using AlbyOnContainers.Kernel.Observability.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace AlbyOnContainers.Kernel.Observability;

public static class ObservabilityKernelExtensions
{
    public static IKernelBuilder WithObservability(this IKernelBuilder builder, string sectionName = ObservabilityOptions.SectionName)
    {
        builder.Host.Services.AddOptions<ObservabilityOptions>()
            .BindConfiguration(sectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.AddInternalObservability(typeof(ObservabilityKernelExtensions).Assembly);
        return builder;
    }

    public static IKernelBuilder WithObservability(this IKernelBuilder builder, Action<ObservabilityOptions> configureOptions)
    {
        builder.Host.Services.AddOptions<ObservabilityOptions>()
            .Configure(configureOptions)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.AddInternalObservability(typeof(ObservabilityKernelExtensions).Assembly);
        return builder;
    }

    public static IKernelBuilder WithObservability<TMarker>(this IKernelBuilder builder, string sectionName = ObservabilityOptions.SectionName)
    {
        builder.Host.Services.AddOptions<ObservabilityOptions>()
            .BindConfiguration(sectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.AddInternalObservability(typeof(TMarker).Assembly);
        return builder;
    }

    private static IKernelBuilder AddInternalObservability(this IKernelBuilder builder, System.Reflection.Assembly scanAssembly)
    {
        builder.ConfigureOpenTelemetry(scanAssembly);
        builder.AddDefaultHealthChecks();
        
        builder.Host.Services.AddServiceDiscovery();
        builder.Host.Services.ConfigureHttpClientDefaults(http =>
        {
            http.AddStandardResilienceHandler();
            http.AddServiceDiscovery();
        });

        return builder;
    }

    private static IKernelBuilder ConfigureOpenTelemetry(this IKernelBuilder builder, System.Reflection.Assembly scanAssembly)
    {
        builder.Host.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
            logging.ParseStateValues = true;
        });

        builder.Host.Services.AddOpenTelemetry()
            .ConfigureResource(resource =>
            {
                var sp = builder.Host.Services.BuildServiceProvider();
                var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ObservabilityOptions>>().Value;

                resource.AddService(
                    serviceName: options.ServiceName,
                    serviceNamespace: options.Namespace,
                    serviceInstanceId: System.Environment.MachineName);
                
                resource.AddEnvironmentVariableDetector();
            })
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();

                var sp = builder.Host.Services.BuildServiceProvider();
                var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ObservabilityOptions>>().Value;

                foreach (var meter in options.CustomMeters)
                {
                    metrics.AddMeter(meter);
                }
                
                if (scanAssembly.GetName().Name is { } assemblyName && !options.CustomMeters.Contains(assemblyName))
                {
                    metrics.AddMeter(assemblyName);
                }
            })
            .WithTracing(tracing =>
            {
                tracing.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddEntityFrameworkCoreInstrumentation();

                var sp = builder.Host.Services.BuildServiceProvider();
                var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ObservabilityOptions>>().Value;

                tracing.AddSource(options.ServiceName);

                foreach (var source in options.CustomTracingSources)
                {
                    tracing.AddSource(source);
                }
                
                if (scanAssembly.GetName().Name is { } assemblyName && !options.CustomTracingSources.Contains(assemblyName))
                {
                    tracing.AddSource(assemblyName);
                }
            });

        var spExporter = builder.Host.Services.BuildServiceProvider();
        var opt = spExporter.GetRequiredService<Microsoft.Extensions.Options.IOptions<ObservabilityOptions>>().Value;
        
        var hasEndpoint = !string.IsNullOrWhiteSpace(opt.OtlpEndpoint) || !string.IsNullOrWhiteSpace(builder.Host.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);
        if (opt.EnableOtlpExporter && hasEndpoint)
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