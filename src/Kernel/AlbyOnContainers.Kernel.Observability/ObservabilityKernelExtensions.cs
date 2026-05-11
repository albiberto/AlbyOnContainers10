using System.ComponentModel.DataAnnotations;
using AlbyOnContainers.Kernel.Observability.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace AlbyOnContainers.Kernel.Observability;

/// <summary>
///     Fluent extensions to wire OpenTelemetry (logs, metrics, traces) on the Kernel builder along
///     with the standard ASP.NET Core resilience handler, service discovery, and the kernel's
///     liveness/readiness probe contract (<see cref="MapKernelObservabilityEndpoints" />).
/// </summary>
public static class ObservabilityKernelExtensions
{
    /// <summary>
    ///     Singleton sentinel registered on the very first <c>WithObservability</c> call. Subsequent
    ///     calls become no-ops, preventing duplicate OpenTelemetry registrations and HTTP client
    ///     defaults stacking up.
    /// </summary>
    private sealed class ObservabilityRegistered;

    // ==============================================================================
    // PUBLIC API (Fluent Builder)
    // ==============================================================================

    extension(IKernelBuilder builder)
    {
        /// <summary>Registers observability binding <see cref="ObservabilityOptions" /> from configuration.</summary>
        public IKernelBuilder WithObservability(string? section = null)
        {
            if (builder.IsObservabilityAlreadyRegistered()) return builder;

            builder.BindOptions(section);
            return AddInternalObservability(
                builder,
                typeof(ObservabilityKernelExtensions).Assembly,
                ResolveStartupOptions(builder, section, configure: null));
        }

        /// <summary>Registers observability configuring <see cref="ObservabilityOptions" /> via a lambda.</summary>
        public IKernelBuilder WithObservability(Action<ObservabilityOptions> configureOptions)
        {
            if (builder.IsObservabilityAlreadyRegistered()) return builder;

            builder.ConfigureOptions(configureOptions);
            return AddInternalObservability(
                builder,
                typeof(ObservabilityKernelExtensions).Assembly,
                ResolveStartupOptions(builder, section: null, configureOptions));
        }

        /// <summary>
        ///     Registers observability and uses <typeparamref name="TMarker" />'s assembly as the source for
        ///     ActivitySource and Meter discovery (the assembly name itself is auto-registered).
        /// </summary>
        public IKernelBuilder WithObservability<TMarker>(string? section = null)
        {
            if (builder.IsObservabilityAlreadyRegistered()) return builder;

            builder.BindOptions(section);
            return AddInternalObservability(
                builder,
                typeof(TMarker).Assembly,
                ResolveStartupOptions(builder, section, configure: null));
        }

        /// <summary>
        ///     Registers observability with a configuration lambda and a marker assembly for
        ///     ActivitySource/Meter discovery.
        /// </summary>
        public IKernelBuilder WithObservability<TMarker>(Action<ObservabilityOptions> configureOptions)
        {
            if (builder.IsObservabilityAlreadyRegistered()) return builder;

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

        private bool IsObservabilityAlreadyRegistered()
        {
            if (builder.Host.Services.Any(s => s.ServiceType == typeof(ObservabilityRegistered))) return true;
            builder.Host.Services.AddSingleton<ObservabilityRegistered>();
            return false;
        }
    }

    // ==============================================================================
    // PUBLIC ENDPOINTS API
    // ==============================================================================

    /// <summary>
    ///     Maps the kernel's three health endpoints in a K8s-friendly contract:
    ///     <list type="bullet">
    ///         <item><c>/alive</c> — liveness probe; returns the status of checks tagged <c>"live"</c>.
    ///         Cheap, never depends on external resources. Failing → K8s restarts the pod.</item>
    ///         <item><c>/ready</c> — readiness probe; returns the status of checks tagged <c>"ready"</c>
    ///         (DB, Redis, broker, ...). Failing → K8s removes the pod from the load balancer (no restart).</item>
    ///         <item><c>/health</c> — aggregate of every registered check, useful for debugging.</item>
    ///     </list>
    /// </summary>
    public static WebApplication MapKernelObservabilityEndpoints(this WebApplication app)
    {
        app.MapHealthChecks("/alive", new()
        {
            Predicate = r => r.Tags.Contains("live")
        }).DisableHttpMetrics();

        app.MapHealthChecks("/ready", new()
        {
            Predicate = r => r.Tags.Contains("ready")
        }).DisableHttpMetrics();

        app.MapHealthChecks("/health").DisableHttpMetrics();

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

        // Eagerly validate the snapshot. Instrumentation registration uses these values immediately
        // (e.g. TraceIdRatioBasedSampler's ctor throws ArgumentOutOfRangeException on bad probability),
        // so we must fail fast HERE with a clean OptionsValidationException instead of an obscure
        // ArgumentException happening deep inside the OpenTelemetry builder.
        var failures = new List<ValidationResult>();
        if (Validator.TryValidateObject(options, new ValidationContext(options), failures, validateAllProperties: true))
            return options;

        throw new OptionsValidationException(
            ObservabilityOptions.Section,
            typeof(ObservabilityOptions),
            failures.Select(f => f.ErrorMessage ?? "Observability options failed validation."));
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

        // ARCHITECTURAL NOTE:
        // Instrumentation registrations (AddAspNetCoreInstrumentation, AddHttpClientInstrumentation,
        // AddRuntimeInstrumentation, AddEntityFrameworkCoreInstrumentation) internally call
        // TracerProviderBuilder.ConfigureServices(...) / MeterProviderBuilder.ConfigureServices(...),
        // which is ONLY valid during the OpenTelemetry builder phase (WithTracing/WithMetrics) —
        // i.e. BEFORE the IServiceProvider is built.
        //
        // Using ConfigureOpenTelemetryTracerProvider / ConfigureOpenTelemetryMeterProvider (which
        // run AFTER SP construction) for these calls throws:
        //   "Services cannot be configured after ServiceProvider has been created."
        //
        // We therefore register instrumentation at build-time using the resolved 'startupOptions'
        // snapshot. The runtime IOptions<ObservabilityOptions> binding is still used by the
        // ResourceDetector (resolved from the SP at build time, which is allowed).
        var otel = builder.Host.Services.AddOpenTelemetry()
            .ConfigureResource(resource =>
            {
                resource.AddEnvironmentVariableDetector();
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddMeter(
                        "Microsoft.AspNetCore.Hosting",
                        "Microsoft.AspNetCore.Server.Kestrel",
                        "System.Net.Http",
                        "System.Net.NameResolution");

                if (startupOptions.EnableHttpClientTracing) metrics.AddHttpClientInstrumentation();

                // MassTransit emits its own Meter ("MassTransit") in v8+. Subscribe so messages
                // published/consumed counters land in the metric pipeline — no custom code needed.
                if (startupOptions.EnableMassTransitTracing) metrics.AddMeter("MassTransit");

                foreach (var meter in startupOptions.CustomMeters) metrics.AddMeter(meter);

                if (scanAssembly.GetName().Name is { } assemblyName && !startupOptions.CustomMeters.Contains(assemblyName))
                    metrics.AddMeter(assemblyName);
            })
            .WithTracing(tracing =>
            {
                // Parent-based ratio sampler is the gold standard for distributed tracing:
                // child spans inherit the parent's sampling decision so a single trace stays
                // either fully captured or fully dropped, regardless of the local probability.
                tracing.SetSampler(new ParentBasedSampler(new TraceIdRatioBasedSampler(startupOptions.TraceSamplingProbability)));

                tracing.AddAspNetCoreInstrumentation();
                if (startupOptions.EnableHttpClientTracing) tracing.AddHttpClientInstrumentation();
                if (startupOptions.EnableEntityFrameworkTracing) tracing.AddEntityFrameworkCoreInstrumentation();

                if (startupOptions.EnableMassTransitTracing)
                    tracing.AddSource("MassTransit");

                foreach (var source in startupOptions.CustomTracingSources) tracing.AddSource(source);

                if (scanAssembly.GetName().Name is { } assemblyName &&
                    !startupOptions.CustomTracingSources.Contains(assemblyName) &&
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
            otel.UseOtlpExporter();
    }

    private static void AddDefaultHealthChecks(IKernelBuilder builder)
    {
        builder.Host.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);
    }
}
