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
    public static IKernelBuilder WithCaching(this IKernelBuilder builder, params Assembly[] scanAssemblies)
    {
        builder.Host.Services.AddOptions<CachingOptions>()
            .BindConfiguration(CachingOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.AddInternalCaching(scanAssemblies);
        return builder;
    }

    public static IKernelBuilder WithCaching(this IKernelBuilder builder, Action<CachingOptions> configureOptions, params Assembly[] scanAssemblies)
    {
        builder.Host.Services.AddOptions<CachingOptions>()
            .Configure(configureOptions)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.AddInternalCaching(scanAssemblies);
        return builder;
    }

    private static void AddInternalCaching(this IKernelBuilder builder, Assembly[] scanAssemblies)
    {
        builder.Host.Services.AddOptions<RedisBackplaneOptions>()
            .Configure<IOptions<CachingOptions>>((options, cachingOptions) =>
            {
                options.Configuration = cachingOptions.Value.RedisConnectionString;
            });

        builder.Host.Services.AddFusionCacheStackExchangeRedisBackplane();
        builder.Host.Services.AddFusionCacheNeueccMessagePackSerializer();

        builder.Host.Services.AddFusionCache()
            .WithDefaultEntryOptions(options =>
            {
                options.Duration = TimeSpan.FromMinutes(30);
                options.IsFailSafeEnabled = true;
                options.FailSafeMaxDuration = TimeSpan.FromHours(2);
                options.JitterMaxDuration = TimeSpan.FromSeconds(2);
            })
            .WithRegisteredBackplane();

        var assemblies = scanAssemblies.Length > 0 ? scanAssemblies : [Assembly.GetCallingAssembly()];

        builder.Host.Services.Scan(scan => scan
            .FromAssemblies(assemblies)
            .AddClasses(classes => classes.AssignableTo(typeof(CacheBase<>)))
            .AsSelf()
            .WithSingletonLifetime());
    }
}