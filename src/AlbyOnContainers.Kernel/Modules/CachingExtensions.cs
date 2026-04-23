using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Backplane.StackExchangeRedis;

namespace AlbyOnContainers.Kernel.Modules;

public static class CachingExtensions
{
    /// <summary>
    /// Configures FusionCache for multi-level caching with optional Redis backplane.
    /// Centralizes the caching strategy and prefix naming across the enterprise.
    /// </summary>
    /// <param name="builder">The kernel builder.</param>
    /// <param name="cacheKeyPrefix">Optional cache key prefix. If not provided, defaults to the application name.</param>
    /// <returns>The kernel builder for chaining.</returns>
    public static IAlbyKernelBuilder WithCaching(this IAlbyKernelBuilder builder, string? cacheKeyPrefix = null)
    {
        // Use provided prefix, otherwise fallback to standard ApplicationName + ":"
        var prefix = cacheKeyPrefix ?? $"{builder.HostBuilder.Environment.ApplicationName}:";
        // Remove whitespace and lowercase for consistency
        prefix = prefix.Replace(" ", "").ToLowerInvariant();
        if (!prefix.EndsWith(':')) prefix += ":";

        var fusionCacheBuilder = builder.Services.AddFusionCache()
            .WithCacheKeyPrefix(prefix)
            .WithDefaultEntryOptions(new FusionCacheEntryOptions
            {
                Duration = TimeSpan.FromMinutes(30),
                IsFailSafeEnabled = true,
                FailSafeMaxDuration = TimeSpan.FromHours(2)
            });

        var redisConnectionString = builder.Configuration.GetConnectionString("cache");

        if (!string.IsNullOrEmpty(redisConnectionString))
        {
            var options = new RedisBackplaneOptions
            {
                Configuration = redisConnectionString
            };
            fusionCacheBuilder.WithBackplane(new RedisBackplane(options));
        }

        return builder;
    }
}
