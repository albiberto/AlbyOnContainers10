using AlbyOnContainers.Kernel.Messaging.Filters;
using MassTransit;
using Microsoft.Extensions.Configuration;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class MessagingServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddMassTransitDefaults(IConfiguration configuration, Action<IBusRegistrationConfigurator>? configureBus = null)
        {
            var connectionString = configuration.GetConnectionString("messaging") ?? throw new InvalidOperationException("Connection string 'messaging' not found. Distributed messaging cannot be established.");

            services.AddMassTransit(x =>
            {
                x.SetKebabCaseEndpointNameFormatter();
                x.DisableUsageTelemetry();

                configureBus?.Invoke(x);

                x.UsingRabbitMq((context, cfg) =>
                {
                    cfg.Host(connectionString);

                    cfg.UseConsumeFilter(typeof(ConsumeTelemetryFilter<>), context);
                    cfg.UseConsumeFilter(typeof(GlobalExceptionFilter<>), context);
                    cfg.UseConsumeFilter(typeof(ValidationFilter<>), context);

                    cfg.ConfigureEndpoints(context);
                });
            });

            return services;
        }

        public IServiceCollection AddMediatorDefaults(Action<IMediatorRegistrationConfigurator> configureConsumers)
        {
            services.AddMediator(cfg =>
            {
                configureConsumers(cfg);

                cfg.ConfigureMediator((context, mcfg) =>
                {
                    mcfg.UseConsumeFilter(typeof(ConsumeTelemetryFilter<>), context);
                    mcfg.UseConsumeFilter(typeof(GlobalExceptionFilter<>), context);
                    mcfg.UseConsumeFilter(typeof(ValidationFilter<>), context);
                });
            });

            return services;
        }
    }
}