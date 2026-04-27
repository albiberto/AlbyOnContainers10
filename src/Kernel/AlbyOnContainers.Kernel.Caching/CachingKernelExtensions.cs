using System;
using AlbyOnContainers.Kernel.Caching.Cache;
using AlbyOnContainers.Kernel.Caching.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Backplane.StackExchangeRedis;

namespace AlbyOnContainers.Kernel.Caching;

public static class CachingKernelExtensions
{
    // ==============================================================================
    // PUBLIC API
    // ==============================================================================

    extension(IKernelBuilder builder)
    {
        public IKernelBuilder WithCaching<TMarker>(string? section = null)
        {
            builder.BindOptions(section);
            builder.ConfigureCaching<TMarker>();
            return builder;
        }

        public IKernelBuilder WithCaching<TMarker>(Action<CachingOptions> configureOptions)
        {
            builder.ConfigureOptions(configureOptions);
            builder.ConfigureCaching<TMarker>();
            return builder;
        }
        
        // ==============================================================================
        // PRIVATE BOILERPLATE HELPERS
        // ==============================================================================

        private void BindOptions(string? section)
        {
            builder.Host.Services
                .AddOptions<CachingOptions>()
                .BindConfiguration(section ?? CachingOptions.Section)
                .ValidateDataAnnotations()
                .ValidateOnStart();
        }

        private void ConfigureOptions(Action<CachingOptions> configure)
        {
            builder.Host.Services
                .AddOptions<CachingOptions>()
                .Configure(configure)
                .ValidateDataAnnotations()
                .ValidateOnStart();
        }

        private void ConfigureCaching<TMarker>()
        {
            var services = builder.Host.Services;

            // 1. Redis Backplane Configuration (Mapped from CachingOptions)
            services.AddOptions<RedisBackplaneOptions>()
                .Configure<IOptions<CachingOptions>>((redis, opt) => 
                { 
                    redis.Configuration = opt.Value.RedisConnectionString; 
                });

            // 2. FusionCache Options Mapping
            services.AddOptions<FusionCacheOptions>()
                .Configure<IOptions<CachingOptions>>((fusion, opt) =>
                {
                    fusion.DefaultEntryOptions.Duration = opt.Value.Duration;
                    fusion.DefaultEntryOptions.IsFailSafeEnabled = opt.Value.IsFailSafeEnabled;
                    fusion.DefaultEntryOptions.FailSafeMaxDuration = opt.Value.FailSafeMaxDuration;
                    fusion.DefaultEntryOptions.JitterMaxDuration = opt.Value.JitterMaxDuration;
                });

            // 3. Infrastructure Registration (Redis + MessagePack)
            services.AddFusionCacheStackExchangeRedisBackplane();
            services.AddFusionCacheNeueccMessagePackSerializer();

            services.AddFusionCache()
                .WithRegisteredBackplane();

            // 4. Auto-Discovery: Scan the assembly for classes inheriting from CacheBase<T>
            services.Scan(scan => scan
                .FromAssembliesOf(typeof(TMarker))
                .AddClasses(classes => classes.AssignableTo(typeof(CacheBase<>)))
                .AsSelf()
                .WithSingletonLifetime());
        }
    }
}