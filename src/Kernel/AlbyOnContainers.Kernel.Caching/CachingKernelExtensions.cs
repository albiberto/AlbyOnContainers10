using AlbyOnContainers.Kernel.Caching.Cache;
using AlbyOnContainers.Kernel.Caching.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Backplane.StackExchangeRedis;

namespace AlbyOnContainers.Kernel.Caching;

public static class CachingKernelExtensions
{
    public static IKernelBuilder WithCaching(this IKernelBuilder builder, Action<CachingOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        builder.ConfigureFusionCache(configure);
        builder.Services.AddSingleton<IAlbyCache, AlbyCache>();

        return builder;
    }

    public static IKernelBuilder WithKeyedCaching(this IKernelBuilder builder, string serviceKey, Action<CachingOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceKey);
        ArgumentNullException.ThrowIfNull(configure);

        builder.ConfigureFusionCache(configure);
        builder.Services.AddKeyedSingleton<IAlbyCache, AlbyCache>(serviceKey);

        return builder;
    }

    private static void ConfigureFusionCache(this IKernelBuilder builder, Action<CachingOptions> configure)
    {
        var services = builder.Services;

        services
            .AddOptions<CachingOptions>()
            .Configure(configure)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<RedisBackplaneOptions>()
            .Configure<IOptions<CachingOptions>>((redis, options) =>
            {
                redis.Configuration = options.Value.RedisConnectionString;
            });

        services.AddOptions<FusionCacheOptions>()
            .Configure<IOptions<CachingOptions>>((fusion, options) =>
            {
                fusion.DefaultEntryOptions.Duration = options.Value.Duration;
                fusion.DefaultEntryOptions.IsFailSafeEnabled = options.Value.IsFailSafeEnabled;
                fusion.DefaultEntryOptions.FailSafeMaxDuration = options.Value.FailSafeMaxDuration;
                fusion.DefaultEntryOptions.JitterMaxDuration = options.Value.JitterMaxDuration;
            });

        services.AddFusionCacheStackExchangeRedisBackplane();
        services.AddFusionCacheNeueccMessagePackSerializer();

        services.AddFusionCache()
            .WithRegisteredBackplane();
    }
}
