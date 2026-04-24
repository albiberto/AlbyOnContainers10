using ZiggyCreatures.Caching.Fusion;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class MessagingServiceCollectionExtensions
{
    public static IServiceCollection AddAlbyCachingDefaults(this IServiceCollection services, IConfiguration configuration)
    {
        var redisConnection = configuration.GetConnectionString("cache") ?? throw new InvalidOperationException("Connection string 'cache' not found. Distributed Caching cannot be established.");

        services.AddFusionCacheStackExchangeRedisBackplane(options => { options.Configuration = redisConnection; });

        services.AddFusionCache().WithDefaultEntryOptions(options =>
            {
                options.Duration = TimeSpan.FromMinutes(30);
                options.IsFailSafeEnabled = true;
                options.FailSafeMaxDuration = TimeSpan.FromHours(2);

                options.JitterMaxDuration = TimeSpan.FromSeconds(2);
            })
            .WithRegisteredBackplane();

        return services;
    }
}