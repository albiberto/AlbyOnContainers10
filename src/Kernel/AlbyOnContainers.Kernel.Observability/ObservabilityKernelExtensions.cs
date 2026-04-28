using System;
using AlbyOnContainers.Kernel.Observability.Detectors;
using AlbyOnContainers.Kernel.Observability.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace AlbyOnContainers.Kernel.Observability;

public static class ObservabilityKernelExtensions
{
    // ==============================================================================
    // PUBLIC API (Fluent Builder)
    // ==============================================================================
    
    extension(IKernelBuilder builder)
    {
        public IKernelBuilder WithObservability(string? section = null)
        {
            builder.Host.Services.AddOptions<ObservabilityOptions>()
                .BindConfiguration(section ?? ObservabilityOptions.Section)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            var options = EvaluateOptions(builder, null, section);

            return AddInternalObservability(builder, typeof(ObservabilityKernelExtensions).Assembly, options);
        }

        public IKernelBuilder WithObservability(Action<ObservabilityOptions> configureOptions)
        {
            builder.Host.Services
                .AddOptions<ObservabilityOptions>()
                .Configure(configureOptions)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            var options = EvaluateOptions(builder, configureOptions, null);

            return AddInternalObservability(builder, typeof(ObservabilityKernelExtensions).Assembly, options);
        }

        public IKernelBuilder WithObservability<TMarker>(string? section = null)
        {
            builder.Host.Services
                .AddOptions<ObservabilityOptions>()
                .BindConfiguration(section ?? ObservabilityOptions.Section)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            var options = EvaluateOptions(builder, null, section);

            return AddInternalObservability(builder, typeof(TMarker).Assembly, options);
        }

        public IKernelBuilder WithObservability<TMarker>(Action<ObservabilityOptions> configureOptions)
        {
            builder.Host.Services
                .AddOptions<ObservabilityOptions>()
                .Configure(configureOptions)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            var options = EvaluateOptions(builder, configureOptions, null);

            return AddInternalObservability(builder, typeof(TMarker).Assembly, options);
        }
    }

    // ==============================================================================
    // PUBLIC ENDPOINTS API
    // ==============================================================================

    public static WebApplication MapKernelObservabilityEndpoints(this WebApplication app)
    {
        app.MapHealthChecks("/health");
        app.MapHealthChecks("/alive", new()
        {
            Predicate = r => r.Tags.Contains("live")
        });
        
        return app;
    }

    // ==============================================================================
    // PRIVATE STATIC HELPERS
    // ==============================================================================

    private static ObservabilityOptions EvaluateOptions(IKernelBuilder builder, Action<ObservabilityOptions>? configure, string? section)
    {
        var options = new ObservabilityOptions();
        builder.Host.Configuration.GetSection(section ?? ObservabilityOptions.Section).Bind(options);
        configure?.Invoke(options);
        return options;
    }

    private static IKernelBuilder AddInternalObservability(IKernelBuilder builder, System.Reflection.Assembly scanAssembly, ObservabilityOptions options)
    {
        ConfigureOpenTelemetry(builder, scanAssembly, options);
        AddDefaultHealthChecks(builder);
    
        builder.Host.Services.AddServiceDiscovery();
        builder.Host.Services.ConfigureHttpClientDefaults(http =>
        {
            http.AddStandardResilienceHandler();
            http.AddServiceDiscovery();
        });

        return builder;
    }

    private static void ConfigureOpenTelemetry(IKernelBuilder builder, System.Reflection.Assembly scanAssembly, ObservabilityOptions options)
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
                resource.AddDetector(sp => new OptionsResourceDetector(sp.GetRequiredService<IOptions<ObservabilityOptions>>().Value));
                resource.AddEnvironmentVariableDetector();
            });

        builder.Host.Services.ConfigureOpenTelemetryMeterProvider((sp, metrics) =>
        {
            var runtimeOptions = sp.GetRequiredService<IOptions<ObservabilityOptions>>().Value;
            
            metrics.AddAspNetCoreInstrumentation().AddRuntimeInstrumentation();
            if (runtimeOptions.EnableHttpClientTracing) metrics.AddHttpClientInstrumentation();
            
            foreach (var meter in runtimeOptions.CustomMeters) metrics.AddMeter(meter);
            
            if (scanAssembly.GetName().Name is { } assemblyName && !runtimeOptions.CustomMeters.Contains(assemblyName))
                metrics.AddMeter(assemblyName);
        });

        builder.Host.Services.ConfigureOpenTelemetryTracerProvider((sp, tracing) =>
        {
            var runtimeOptions = sp.GetRequiredService<IOptions<ObservabilityOptions>>().Value;

            tracing.AddAspNetCoreInstrumentation();
            if (runtimeOptions.EnableHttpClientTracing) tracing.AddHttpClientInstrumentation();
            if (runtimeOptions.EnableEntityFrameworkTracing) tracing.AddEntityFrameworkCoreInstrumentation();

            tracing.AddSource(runtimeOptions.ServiceName);
            
            if (runtimeOptions.EnableMassTransitTracing)
            {
                tracing.AddSource("MassTransit");
            }

            foreach (var source in runtimeOptions.CustomTracingSources) tracing.AddSource(source);

            if (scanAssembly.GetName().Name is { } assemblyName && 
                !runtimeOptions.CustomTracingSources.Contains(assemblyName) && 
                assemblyName != "MassTransit")
            {
                tracing.AddSource(assemblyName);
            }
        });

        var hasOtlpEnvVar = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT"));

        if (options.EnableOtlpExporter || hasOtlpEnvVar)
        {
            builder.Host.Services.AddOpenTelemetry().UseOtlpExporter();
        }
    }

    private static void AddDefaultHealthChecks(IKernelBuilder builder)
    {
        builder.Host.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);
    }
}