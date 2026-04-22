using AlbyOnContainers.Shared.Application.Infrastructure;
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
    extension(IServiceCollection services)
    {
        public IServiceCollection AddApplication(IConfiguration configuration)
        {
            services.AddMediators();
            services.AddFusionCache(configuration);

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

        private void AddMediators()
        {
            services.AddMediator(configurator =>
            {
                configurator.AddConsumers(typeof(ApplicationServiceExtensions).Assembly);
                configurator.ConfigureMediatorPipeline(); 
            });
        }

        private void AddFusionCache(IConfiguration configuration)
        {
            var fusionCacheBuilder = services.AddFusionCache()
                .WithCacheKeyPrefix("pim:")
                .WithDefaultEntryOptions(new FusionCacheEntryOptions
                {
                    Duration = TimeSpan.FromMinutes(30),
                    IsFailSafeEnabled = true,
                    FailSafeMaxDuration = TimeSpan.FromHours(2)
                });

            var redisConnectionString = configuration.GetConnectionString("cache");

            if (string.IsNullOrEmpty(redisConnectionString)) return;
            
            var options = new RedisBackplaneOptions
            {
                Configuration = redisConnectionString
            };
            fusionCacheBuilder.WithBackplane(new RedisBackplane(options));
        }
    }
}