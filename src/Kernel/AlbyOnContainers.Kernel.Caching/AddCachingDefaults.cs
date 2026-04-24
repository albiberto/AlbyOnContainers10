using System;
using System.Reflection;
using AlbyOnContainers.Kernel.Abstraction;
using AlbyOnContainers.Kernel.Caching.Cache;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ZiggyCreatures.Caching.Fusion;

namespace AlbyOnContainers.Kernel.Caching;

public static class CachingKernelExtensions
{
    public static IKernelBuilder WithCaching(this IKernelBuilder builder, params Assembly[] scanAssemblies)
    {
        var redisConnection = builder.Host.Configuration.GetConnectionString("cache");

        if (string.IsNullOrWhiteSpace(redisConnection))
        {
            throw new InvalidOperationException("Fail-Fast: Connection string 'cache' not found. Distributed Caching cannot be established.");
        }

        builder.Host.Services.AddFusionCacheStackExchangeRedisBackplane(options => { options.Configuration = redisConnection; });
        builder.Host.Services.AddFusionCacheSystemTextJsonSerializer();

        builder.Host.Services.AddFusionCache()
            .WithDefaultEntryOptions(options =>
            {
                options.Duration = TimeSpan.FromMinutes(30);
                options.IsFailSafeEnabled = true;
                options.FailSafeMaxDuration = TimeSpan.FromHours(2);
                options.JitterMaxDuration = TimeSpan.FromSeconds(2);
            })
            .WithRegisteredBackplane();

        var assembliesToScan = scanAssemblies.Length > 0 ? scanAssemblies : [Assembly.GetCallingAssembly()];

        builder.Host.Services.Scan(scan => scan
            .FromAssemblies(assembliesToScan)
            .AddClasses(classes => classes.AssignableTo(typeof(CacheBase<>)))
            .AsSelf()
            .WithSingletonLifetime());

        return builder;
    }
}