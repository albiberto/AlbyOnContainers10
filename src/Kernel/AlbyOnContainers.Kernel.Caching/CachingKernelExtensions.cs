// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

using System;
using AlbyOnContainers.Kernel;
using AlbyOnContainers.Kernel.Caching.Abstractions;
using AlbyOnContainers.Kernel.Caching.Cache;
using AlbyOnContainers.Kernel.Caching.Options;
using Caching.Distributed;
using Caching.StackExchangeRedis;
using Microsoft.Extensions.Hosting;
using Options;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Backplane.StackExchangeRedis;
using ZiggyCreatures.Caching.Fusion.Serialization;

/// <summary>
///     Fluent extensions to register FusionCache (L1 memory + L2 Redis backplane) on the Kernel builder.
/// </summary>
/// <remarks>
///     <para>
///         Every <c>WithCaching</c> overload assumes that an <see cref="StackExchange.Redis.IConnectionMultiplexer" />
///         (or a keyed one for keyed registrations) is already present in the DI container. The recommended way to
///         provide it is Aspire's <c>builder.AddRedisClient("cache")</c>, which must run BEFORE <c>AddKernel()</c>.
///     </para>
///     <para>
///         A <see cref="Cache.CachingBackplaneProbe" /> is registered automatically and validates the multiplexer at
///         application startup, failing fast with an explicit error if the prerequisite is missing.
///     </para>
/// </remarks>
public static class CachingKernelExtensions
{
    // --- PUBLIC FACADE LOGIC ---

    extension(IKernelBuilder builder)
    {
        /// <summary>Registers default caching using configuration binding.</summary>
        public IKernelBuilder WithCaching(string? configurationSection = null)
        {
            var section = configurationSection ?? CachingOptions.Section;

            builder.Services.BindOptions(Options.DefaultName, section);
            builder.Services.AddFusionCacheInternal(Options.DefaultName);
            builder.Services.AddSingleton<ICache, Cache>();

            return builder;
        }

        /// <summary>Registers default caching using a configuration lambda.</summary>
        public IKernelBuilder WithCaching(Action<CachingOptions> configureOptions)
        {
            ArgumentNullException.ThrowIfNull(configureOptions);

            builder.Services.ConfigureOptions(Options.DefaultName, configureOptions);
            builder.Services.AddFusionCacheInternal(Options.DefaultName);
            builder.Services.AddSingleton<ICache, Cache>();

            return builder;
        }

        /// <summary>Registers a keyed caching pipeline bound from configuration.</summary>
        public IKernelBuilder WithKeyedCaching(string serviceKey, string? configurationSection = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(serviceKey);

            var section = configurationSection ?? $"{CachingOptions.Section}:{serviceKey}";

            builder.Services.BindOptions(serviceKey, section);
            builder.Services.AddFusionCacheInternal(serviceKey);
            builder.Services.AddKeyedCacheBridge(serviceKey);

            return builder;
        }

        /// <summary>Registers a keyed caching pipeline using a configuration lambda.</summary>
        public IKernelBuilder WithKeyedCaching(string serviceKey, Action<CachingOptions> configureOptions)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(serviceKey);
            ArgumentNullException.ThrowIfNull(configureOptions);

            builder.Services.ConfigureOptions(serviceKey, configureOptions);
            builder.Services.AddFusionCacheInternal(serviceKey);
            builder.Services.AddKeyedCacheBridge(serviceKey);

            return builder;
        }
    }

    // --- PRIVATE INFRASTRUCTURE HELPERS ---

    extension(IServiceCollection services)
    {
        // Bridges a keyed ICache to the named IFusionCache produced by AddFusionCache(name)
        // by resolving the cache through IFusionCacheProvider.
        private void AddKeyedCacheBridge(string serviceKey) =>
            services.AddKeyedSingleton<ICache>(serviceKey, (sp, key) =>
                new Cache(sp.GetRequiredService<IFusionCacheProvider>().GetCache((string)key!)));

        private void BindOptions(string name, string section) =>
            services.AddOptions<CachingOptions>(name)
                .BindConfiguration(section)
                .ValidateDataAnnotations()
                .ValidateOnStart();

        private void ConfigureOptions(string name, Action<CachingOptions> configureOptions) =>
            services.AddOptions<CachingOptions>(name)
                .Configure(configureOptions)
                .ValidateDataAnnotations()
                .ValidateOnStart();

        private void AddFusionCacheInternal(string name)
        {
            services.AddOptions<FusionCacheOptions>(name)
                .Configure<IOptionsMonitor<CachingOptions>>((fusion, optionsMonitor) =>
                {
                    var options = optionsMonitor.Get(name);
                    fusion.DefaultEntryOptions.Duration = options.Duration;
                    fusion.DefaultEntryOptions.IsFailSafeEnabled = options.IsFailSafeEnabled;
                    fusion.DefaultEntryOptions.FailSafeMaxDuration = options.FailSafeMaxDuration;
                    fusion.DefaultEntryOptions.JitterMaxDuration = options.JitterMaxDuration;
                });

            services.AddOptions<RedisBackplaneOptions>(name)
                .Configure<IServiceProvider>((redis, sp) =>
                {
                    redis.ConnectionMultiplexerFactory = () =>
                    {
                        var multiplexer = name == Options.DefaultName
                            ? sp.GetRequiredService<StackExchange.Redis.IConnectionMultiplexer>()
                            : sp.GetRequiredKeyedService<StackExchange.Redis.IConnectionMultiplexer>(name);

                        return Task.FromResult(multiplexer);
                    };
                });

            var cacheBuilder = name == Options.DefaultName
                ? services.AddFusionCache()
                : services.AddFusionCache(name);

            services.AddFusionCacheStackExchangeRedisBackplane();
            services.AddFusionCacheNeueccMessagePackSerializer();

            cacheBuilder.WithRegisteredBackplane();

            // L2 distributed cache (Redis) — wired for both default and keyed pipelines.
            if (name == Options.DefaultName)
            {
                // Default path uses AddStackExchangeRedisCache, which registers a singleton
                // IDistributedCache backed by the non-keyed IConnectionMultiplexer.
                services.AddOptions<RedisCacheOptions>()
                    .Configure<IServiceProvider>((redis, sp) =>
                    {
                        redis.ConnectionMultiplexerFactory = () =>
                            Task.FromResult(sp.GetRequiredService<StackExchange.Redis.IConnectionMultiplexer>());
                    });

                services.AddStackExchangeRedisCache(_ => { });

                cacheBuilder
                    .WithRegisteredDistributedCache()
                    .WithRegisteredSerializer();
            }
            else
            {
                // Keyed path: AddStackExchangeRedisCache cannot register a keyed IDistributedCache,
                // so we instantiate a RedisCache per pipeline using the keyed multiplexer and bind
                // it to the FusionCache builder with WithDistributedCache(sp => keyed).
                services.AddKeyedSingleton<IDistributedCache>(name, (sp, key) =>
                {
                    var multiplexer = sp.GetRequiredKeyedService<StackExchange.Redis.IConnectionMultiplexer>((string)key!);
                    var redisOptions = Microsoft.Extensions.Options.Options.Create(new RedisCacheOptions
                    {
                        ConnectionMultiplexerFactory = () => Task.FromResult(multiplexer)
                    });
                    return new RedisCache(redisOptions);
                });

                cacheBuilder
                    .WithDistributedCache(sp => sp.GetRequiredKeyedService<IDistributedCache>(name))
                    .WithRegisteredSerializer();
            }

            services.AddSingleton<IHostedService>(sp => new CachingBackplaneProbe(sp, name == Options.DefaultName ? null : name));

            // Health check on the Redis multiplexer (default or keyed) tagged "ready":
            // the K8s readiness probe will pull the pod out of rotation if Redis becomes unreachable.
            // Uses AspNetCore.HealthChecks.Redis (.NET Foundation) — no custom code.
            services.AddHealthChecks().AddRedis(
                connectionMultiplexerFactory: sp => name == Options.DefaultName
                    ? sp.GetRequiredService<StackExchange.Redis.IConnectionMultiplexer>()
                    : sp.GetRequiredKeyedService<StackExchange.Redis.IConnectionMultiplexer>(name),
                name: name == Options.DefaultName ? "redis" : $"redis-{name}",
                tags: ["ready"]);
        }
    }
}
