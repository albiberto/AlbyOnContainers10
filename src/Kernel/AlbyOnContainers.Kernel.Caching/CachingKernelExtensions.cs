namespace Microsoft.Extensions.DependencyInjection;

using System;
using AlbyOnContainers.Kernel;
using AlbyOnContainers.Kernel.Caching.Options;
using Microsoft.Extensions.Options;
using OpenTelemetry.Metrics;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Serialization;

public static class CachingKernelExtensions
{
    public static IKernelBuilder WithCaching(this IKernelBuilder builder, string? configurationSection = null)
    {
        var section = configurationSection ?? CachingOptions.Section;

        builder.Services.AddOptions<CachingOptions>(Options.DefaultName)
            .BindConfiguration(section)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services.AddOptions<FusionCacheOptions>(Options.DefaultName)
            .Configure<IOptionsMonitor<CachingOptions>>((fusion, optionsMonitor) =>
            {
                var options = optionsMonitor.Get(Options.DefaultName);
                fusion.DefaultEntryOptions.Duration = options.Duration;
                fusion.DefaultEntryOptions.IsFailSafeEnabled = options.IsFailSafeEnabled;
                fusion.DefaultEntryOptions.FailSafeMaxDuration = options.FailSafeMaxDuration;
                fusion.DefaultEntryOptions.JitterMaxDuration = options.JitterMaxDuration;
            });

        builder.Services.AddFusionCache()
            .WithRegisteredSerializer();

        builder.Services.AddFusionCacheNeueccMessagePackSerializer();

        // Native observability.
        builder.Services.AddOpenTelemetry().WithMetrics(m => m.AddMeter("ZiggyCreatures.Caching.Fusion"));

        return builder;
    }
}
