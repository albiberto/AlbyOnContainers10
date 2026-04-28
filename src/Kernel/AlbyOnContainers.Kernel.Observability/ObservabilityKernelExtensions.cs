using AlbyOnContainers.Kernel.Observability.Detectors;
using AlbyOnContainers.Kernel.Observability.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace AlbyOnContainers.Kernel.Observability;

using Microsoft.Extensions.Configuration;

public static class ObservabilityKernelExtensions
{
    extension(IKernelBuilder builder)
    {
        public IKernelBuilder WithObservability(string? section = null)
        {
            builder.Host.Services.AddOptions<ObservabilityOptions>()
                .BindConfiguration(section ?? ObservabilityOptions.Section)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            builder.AddInternalObservability(typeof(ObservabilityKernelExtensions).Assembly);
            return builder;
        }

        public IKernelBuilder WithObservability(Action<ObservabilityOptions> configureOptions)
        {
            builder.Host.Services
                .AddOptions<ObservabilityOptions>()
                .Configure(configureOptions)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            builder.AddInternalObservability(typeof(ObservabilityKernelExtensions).Assembly);
            return builder;
        }

        public IKernelBuilder WithObservability<TMarker>(string? section = null)
        {
            builder.Host.Services
                .AddOptions<ObservabilityOptions>()
                .BindConfiguration(section ?? ObservabilityOptions.Section)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            builder.AddInternalObservability(typeof(TMarker).Assembly);
            return builder;
        }

        public IKernelBuilder WithObservability<TMarker>(Action<ObservabilityOptions> configureOptions)
        {
            builder.Host.Services
                .AddOptions<ObservabilityOptions>()
                .Configure(configureOptions)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            builder.AddInternalObservability(typeof(TMarker).Assembly);
            return builder;
        }

        private IKernelBuilder AddInternalObservability(System.Reflection.Assembly scanAssembly)
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

        private IKernelBuilder ConfigureOpenTelemetry(System.Reflection.Assembly scanAssembly)
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
                var options = sp.GetRequiredService<IOptions<ObservabilityOptions>>().Value;
                
                metrics.AddAspNetCoreInstrumentation().AddRuntimeInstrumentation();
                if (options.EnableHttpClientTracing) metrics.AddHttpClientInstrumentation();
                
                foreach (var meter in options.CustomMeters) metrics.AddMeter(meter);
                
                if (scanAssembly.GetName().Name is { } assemblyName && !options.CustomMeters.Contains(assemblyName))
                    metrics.AddMeter(assemblyName);
            });

            builder.Host.Services.ConfigureOpenTelemetryTracerProvider((sp, tracing) =>
            {
                var options = sp.GetRequiredService<IOptions<ObservabilityOptions>>().Value;

                tracing.AddAspNetCoreInstrumentation();
                if (options.EnableHttpClientTracing) tracing.AddHttpClientInstrumentation();
                if (options.EnableEntityFrameworkTracing) tracing.AddEntityFrameworkCoreInstrumentation();

                // 1. Aggiunta Service Name
                tracing.AddSource(options.ServiceName);

                // 2. SCELTA SUPERIORE: I default inamovibili di piattaforma sono cablati qui
                tracing.AddSource("MassTransit");

                // 3. Aggiunta sorgenti custom del microservizio
                foreach (var source in options.CustomTracingSources) tracing.AddSource(source);

                // 4. Auto-discovery dell'assembly corrente
                if (scanAssembly.GetName().Name is { } assemblyName && 
                    !options.CustomTracingSources.Contains(assemblyName) && 
                    assemblyName != "MassTransit")
                {
                    tracing.AddSource(assemblyName);
                }
            });

            var config = builder.Host.Configuration
                .GetSection(ObservabilityOptions.Section)
                .Get<ObservabilityOptions>() ?? new ObservabilityOptions();

            var hasOtlpEnvVar = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT"));

            if (config.EnableOtlpExporter || hasOtlpEnvVar)
            {
                builder.Host.Services.AddOpenTelemetry().UseOtlpExporter();
            }

            return builder;
        }

        private IKernelBuilder AddDefaultHealthChecks()
        {
            builder.Host.Services.AddHealthChecks()
                .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

            return builder;
        }
    }

    public static WebApplication MapKernelObservabilityEndpoints(this WebApplication app)
    {
        app.MapHealthChecks("/health");
        app.MapHealthChecks("/alive", new()
        {
            Predicate = r => r.Tags.Contains("live")
        });
        
        return app;
    }
}