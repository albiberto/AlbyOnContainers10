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
        /// <summary>
        /// Registers FusionCache (L1 memory + L2 Redis backplane) and auto-discovers
        /// every <see cref="CacheBase{TDto}"/> in the assembly of <typeparamref name="TMarker"/>.
        /// </summary>
        public IKernelBuilder WithCaching<TMarker>(string? section = null)
        {
            builder.BindOptions(section);
            builder.ConfigureCaching([typeof(TMarker)]);
            return builder;
        }

        public IKernelBuilder WithCaching<TMarker>(Action<CachingOptions> configureOptions)
        {
            builder.ConfigureOptions(configureOptions);
            builder.ConfigureCaching([typeof(TMarker)]);
            return builder;
        }

        /// <summary>
        /// Multi-assembly variant: scans every assembly identified by <paramref name="assemblyMarkers"/>.
        /// </summary>
        public IKernelBuilder WithCaching(string? section, params Type[] assemblyMarkers)
        {
            ValidateMarkers(assemblyMarkers);
            builder.BindOptions(section);
            builder.ConfigureCaching(assemblyMarkers);
            return builder;
        }

        public IKernelBuilder WithCaching(Action<CachingOptions> configureOptions, params Type[] assemblyMarkers)
        {
            ValidateMarkers(assemblyMarkers);
            builder.ConfigureOptions(configureOptions);
            builder.ConfigureCaching(assemblyMarkers);
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

        private void ConfigureCaching(Type[] assemblyMarkers)
        {
            var services = builder.Host.Services;
            var assemblies = assemblyMarkers.Select(t => t.Assembly).Distinct().ToArray();

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

            // 4. Auto-Discovery: scan ALL marker assemblies for classes inheriting from CacheBase<>
            services.Scan(scan => scan
                .FromAssemblies(assemblies)
                .AddClasses(classes => classes.AssignableTo(typeof(CacheBase<>)))
                .AsSelf()
                .WithSingletonLifetime());
        }
    }

    private static void ValidateMarkers(Type[] markers)
    {
        ArgumentNullException.ThrowIfNull(markers);
        if (markers.Length == 0)
            throw new ArgumentException("At least one marker type must be provided to scan for cache classes.", nameof(markers));
    }
}