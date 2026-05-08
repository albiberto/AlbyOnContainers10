// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

using System;
using AlbyOnContainers.Kernel;
using AlbyOnContainers.Kernel.Caching.Abstractions;
using AlbyOnContainers.Kernel.Caching.Cache;
using AlbyOnContainers.Kernel.Caching.Options;
using Options;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Backplane.StackExchangeRedis;

public static class CachingKernelExtensions
{
    // --- PUBLIC FACADE LOGIC ---

    extension(IKernelBuilder builder)
    {
        // 1. Standard Caching (IConfiguration Binding)
        public IKernelBuilder WithCaching(string? configurationSection = null)
        {
            var section = configurationSection ?? CachingOptions.Section;

            builder.Services.BindOptions(Options.DefaultName, section);
            builder.Services.AddFusionCacheInternal(Options.DefaultName);
            builder.Services.AddSingleton<ICache, Cache>();

            return builder;
        }

        // 2. Standard Caching (Lambda Configuration)
        public IKernelBuilder WithCaching(Action<CachingOptions> configureOptions)
        {
            ArgumentNullException.ThrowIfNull(configureOptions);

            builder.Services.ConfigureOptions(Options.DefaultName, configureOptions);
            builder.Services.AddFusionCacheInternal(Options.DefaultName);
            builder.Services.AddSingleton<ICache, Cache>();

            return builder;
        }

        // 3. Keyed Caching (IConfiguration Binding)
        public IKernelBuilder WithKeyedCaching(string serviceKey, string? configurationSection = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(serviceKey);

            var section = configurationSection ?? $"{CachingOptions.Section}:{serviceKey}";

            builder.Services.BindOptions(serviceKey, section);
            builder.Services.AddFusionCacheInternal(serviceKey);
            builder.Services.AddKeyedSingleton<ICache, Cache>(serviceKey);

            return builder;
        }

        // 4. Keyed Caching (Lambda Configuration)
        public IKernelBuilder WithKeyedCaching(string serviceKey, Action<CachingOptions> configureOptions)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(serviceKey);
            ArgumentNullException.ThrowIfNull(configureOptions);

            builder.Services.ConfigureOptions(serviceKey, configureOptions);
            builder.Services.AddFusionCacheInternal(serviceKey);
            builder.Services.AddKeyedSingleton<ICache, Cache>(serviceKey);

            return builder;
        }
    }

    // --- PRIVATE INFRASTRUCTURE HELPERS ---

    extension(IServiceCollection services)
    {
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
        }
    }
}