using FluentValidation;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using ProductInformationManager.Application.Cache;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Backplane.StackExchangeRedis;

namespace ProductInformationManager.Application;

public static class ApplicationServiceExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMediator(configurator =>
        {
            configurator.AddConsumers(typeof(ApplicationServiceExtensions).Assembly);
            configurator.ConfigureMediator((context, cfg) => { });
        });

        var fusionCacheBuilder = services.AddFusionCache()
            .WithDefaultEntryOptions(new FusionCacheEntryOptions
            {
                Duration = TimeSpan.FromMinutes(30),
                IsFailSafeEnabled = true,
                FailSafeMaxDuration = TimeSpan.FromHours(2)
            });

        var redisConnectionString = configuration.GetConnectionString("cache");

        if (!string.IsNullOrEmpty(redisConnectionString))
        {
            var options = new RedisBackplaneOptions
            {
                Configuration = redisConnectionString
            };
            fusionCacheBuilder.WithBackplane(new RedisBackplane(options));
        }

        services.Scan(scan => scan
            .FromAssemblyOf<CategoryCache>()
            .AddClasses(classes => classes.InNamespaceOf<CategoryCache>())
            .AsSelf()
            .WithSingletonLifetime()
        );

        services.AddValidatorsFromAssembly(typeof(ApplicationServiceExtensions).Assembly);
        services.AddLocalization();

        return services;
    }
}

