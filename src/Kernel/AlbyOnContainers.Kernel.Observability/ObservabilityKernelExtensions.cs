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
            builder.BindOptions(section);
            return AddInternalObservability(
                builder,
                typeof(ObservabilityKernelExtensions).Assembly,
                ResolveStartupOptions(builder, section, configure: null));
        }

        public IKernelBuilder WithObservability(Action<ObservabilityOptions> configureOptions)
        {
            builder.ConfigureOptions(configureOptions);
            return AddInternalObservability(
                builder,
                typeof(ObservabilityKernelExtensions).Assembly,
                ResolveStartupOptions(builder, section: null, configureOptions));
        }

        public IKernelBuilder WithObservability<TMarker>(string? section = null)
        {
            builder.BindOptions(section);
            return AddInternalObservability(
                builder,
                typeof(TMarker).Assembly,
                ResolveStartupOptions(builder, section, configure: null));
        }

        public IKernelBuilder WithObservability<TMarker>(Action<ObservabilityOptions> configureOptions)
        {
            builder.ConfigureOptions(configureOptions);
            return AddInternalObservability(
                builder,
                typeof(TMarker).Assembly,
                ResolveStartupOptions(builder, section: null, configureOptions));
        }

        // ==============================================================================
        // INTERNAL OPTIONS HELPERS
        // ==============================================================================

        private void BindOptions(string? section)
        {
            builder.Host.Services
                .AddOptions<ObservabilityOptions>()
                .BindConfiguration(section ?? ObservabilityOptions.Section)
                .ValidateDataAnnotations()
                .ValidateOnStart();
        }

        private void ConfigureOptions(Action<ObservabilityOptions> configure)
        {
            builder.Host.Services
                .AddOptions<ObservabilityOptions>()
                .Configure(configure)
                .ValidateDataAnnotations()
                .ValidateOnStart();
        }
    }

    // ==============================================================================
    // PUBLIC ENDPOINTS API
    // ==============================================================================

    public static WebApplication MapKernelObservabilityEndpoints(this WebApplication app)
    {
        // Liveness: process is up (cheap probe).
        app.MapHealthChecks("/alive", new()
        {
            Predicate = r => r.Tags.Contains("live")
        });

        // Readiness: process is up AND all dependencies (DB, broker, ...) are reachable.
        app.MapHealthChecks("/ready", new()
        {
            Predicate = r => r.Tags.Contains("ready")
        });

        // Aggregated probe.
        app.MapHealthChecks("/health");

        return app;
    }

    // ==============================================================================
    // PRIVATE STATIC HELPERS
    // ==============================================================================

    /// <summary>
    /// Materializes the ObservabilityOptions ONCE at registration time, so we can take
    /// startup-only decisions (e.g. enabling the OTLP exporter) without ribindering the
    /// options section twice. The runtime DI binding remains the source of truth for
    /// runtime behavior — this snapshot is only used for build-time wiring.
    /// </summary>
    private static ObservabilityOptions ResolveStartupOptions(
        IKernelBuilder builder,
        string? section,
        Action<ObservabilityOptions>? configure)
    {
        var options = new ObservabilityOptions();
        builder.Host.Configuration.GetSection(section ?? ObservabilityOptions.Section).Bind(options);
        configure?.Invoke(options);
        return options;
    }

    private static IKernelBuilder AddInternalObservability(
        IKernelBuilder builder,
        System.Reflection.Assembly scanAssembly,
        ObservabilityOptions startupOptions)
    {
        ConfigureOpenTelemetry(builder, scanAssembly, startupOptions);
        AddDefaultHealthChecks(builder);

        builder.Host.Services.AddServiceDiscovery();
        builder.Host.Services.ConfigureHttpClientDefaults(http =>
        {
            http.AddStandardResilienceHandler();
            http.AddServiceDiscovery();
        });

        return builder;
    }

    private static void ConfigureOpenTelemetry(
        IKernelBuilder builder,
        System.Reflection.Assembly scanAssembly,
        ObservabilityOptions startupOptions)
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

            metrics
                .AddAspNetCoreInstrumentation()
                .AddRuntimeInstrumentation();

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
                tracing.AddSource("MassTransit");

            foreach (var source in runtimeOptions.CustomTracingSources) tracing.AddSource(source);

            if (scanAssembly.GetName().Name is { } assemblyName &&
                !runtimeOptions.CustomTracingSources.Contains(assemblyName) &&
                assemblyName != "MassTransit")
            {
                tracing.AddSource(assemblyName);
            }
        });

        // OTLP exporter wiring is a startup-time decision. We use the snapshot resolved at
        // registration time + the standard OTEL env var. This is the only correct moment to
        // decide because UseOtlpExporter() must be called before the host is built.
        var hasOtlpEnvVar = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT"));
        if (startupOptions.EnableOtlpExporter || hasOtlpEnvVar)
            builder.Host.Services.AddOpenTelemetry().UseOtlpExporter();
    }

    private static void AddDefaultHealthChecks(IKernelBuilder builder)
    {
        builder.Host.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);
    }
}