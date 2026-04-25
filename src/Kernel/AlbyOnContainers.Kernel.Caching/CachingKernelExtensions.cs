using System;
using System.Reflection;
using AlbyOnContainers.Kernel.Abstraction;
using AlbyOnContainers.Kernel.Caching.Cache;
using AlbyOnContainers.Kernel.Caching.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Backplane.StackExchangeRedis;

namespace AlbyOnContainers.Kernel.Caching;

public static class CachingKernelExtensions
{
    public static IKernelBuilder WithCaching(this IKernelBuilder builder, string sectionName = CachingOptions.SectionName)
    {
        builder.Host.Services.AddOptions<CachingOptions>()
            .BindConfiguration(sectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.AddInternalCaching(Assembly.GetCallingAssembly());
        return builder;
    }

    public static IKernelBuilder WithCaching(this IKernelBuilder builder, Action<CachingOptions> configureOptions)
    {
        builder.Host.Services.AddOptions<CachingOptions>()
            .Configure(configureOptions)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.AddInternalCaching(Assembly.GetCallingAssembly());
        return builder;
    }

    public static IKernelBuilder WithCaching<TMarker>(this IKernelBuilder builder, string sectionName = CachingOptions.SectionName)
    {
        builder.Host.Services.AddOptions<CachingOptions>()
            .BindConfiguration(sectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.AddInternalCaching(typeof(TMarker).Assembly);
        return builder;
    }

    private static void AddInternalCaching(this IKernelBuilder builder, Assembly scanAssembly)
    {
        builder.Host.Services.AddOptions<RedisBackplaneOptions>()
            .Configure<IOptions<CachingOptions>>((redis, opt) =>
            {
                redis.Configuration = opt.Value.RedisConnectionString;
            });

        builder.Host.Services.AddOptions<FusionCacheOptions>()
            .Configure<IOptions<CachingOptions>>((fusion, opt) =>
            {
                fusion.DefaultEntryOptions.Duration = TimeSpan.FromMinutes(opt.Value.DurationInMinutes);
                fusion.DefaultEntryOptions.IsFailSafeEnabled = opt.Value.IsFailSafeEnabled;
                fusion.DefaultEntryOptions.FailSafeMaxDuration = TimeSpan.FromHours(opt.Value.FailSafeMaxDurationInHours);
                fusion.DefaultEntryOptions.JitterMaxDuration = TimeSpan.FromSeconds(opt.Value.JitterMaxDurationInSeconds);
            });

        builder.Host.Services.AddFusionCacheStackExchangeRedisBackplane();
        builder.Host.Services.AddFusionCacheNeueccMessagePackSerializer();

        builder.Host.Services.AddFusionCache()
            .WithRegisteredBackplane();

        builder.Host.Services.Scan(scan => scan
            .FromAssemblies(scanAssembly)
            .AddClasses(classes => classes.AssignableTo(typeof(CacheBase<>)))
            .AsSelf()
            .WithSingletonLifetime());
    }
}