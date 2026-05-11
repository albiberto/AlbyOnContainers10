// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

using System;
using System.Threading.Tasks;
using AlbyOnContainers.Kernel;
using Caching.StackExchangeRedis;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Backplane.StackExchangeRedis;

public static class RedisCachingExtensions
{
    public static IKernelBuilder WithRedisBackplane(this IKernelBuilder builder, string connectionName = "cache")
    {
        builder.Services.AddOptions<RedisCacheOptions>()
            .Configure<IServiceProvider>((redis, sp) =>
            {
                redis.ConnectionMultiplexerFactory = () => Task.FromResult(sp.GetRequiredService<StackExchange.Redis.IConnectionMultiplexer>());
            });
        builder.Services.AddStackExchangeRedisCache(_ => { });

        builder.Services.AddOptions<RedisBackplaneOptions>(Options.Options.DefaultName)
            .Configure<IServiceProvider>((redis, sp) =>
            {
                redis.ConnectionMultiplexerFactory = () =>
                    Task.FromResult(sp.GetRequiredService<StackExchange.Redis.IConnectionMultiplexer>());
            });
        builder.Services.AddFusionCacheStackExchangeRedisBackplane();

        builder.Services.AddFusionCache()
            .WithRegisteredBackplane()
            .WithRegisteredDistributedCache()
            .WithRegisteredSerializer();

        builder.Services.AddHealthChecks().AddRedis(
            connectionMultiplexerFactory: sp => sp.GetRequiredService<StackExchange.Redis.IConnectionMultiplexer>(),
            name: "redis",
            tags: ["ready"]);

        return builder;
    }
}
