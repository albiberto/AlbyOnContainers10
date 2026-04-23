using System;
using AlbyOnContainers.Shared.Application.Infrastructure;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AlbyOnContainers.Kernel.Modules;

public static class MessagingExtensions
{
    /// <summary>
    /// Configures MassTransit and RabbitMQ for distributed messaging.
    /// Automatically applies enterprise-grade telemetry, exception handling, and validation filters.
    /// </summary>
    /// <param name="builder">The kernel builder.</param>
    /// <param name="configureRabbitMq">Optional custom RabbitMQ configuration.</param>
    /// <returns>The kernel builder for chaining.</returns>
    public static IAlbyKernelBuilder WithMessaging(
        this IAlbyKernelBuilder builder,
        Action<IBusRegistrationConfigurator>? configureBus = null,
        Action<IBusRegistrationContext, IRabbitMqBusFactoryConfigurator>? configureRabbitMq = null)
    {
        var connectionString = builder.Configuration.GetConnectionString("messaging") 
            ?? throw new InvalidOperationException("Connection string 'messaging' not found. Distributed messaging cannot be established.");

        builder.Services.AddMassTransit(x =>
        {
            // Best Practice: Use a consistent naming convention for queues (Kebab-case)
            x.SetKebabCaseEndpointNameFormatter();

            // Disable MassTransit's default telemetry because we use our custom Telemetry filter
            x.DisableUsageTelemetry();

            configureBus?.Invoke(x);

            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(connectionString);

                // OBBLIGATORIO: Apply the global Kernel filters (Telemetry, Exceptions, Validation)
                cfg.ConfigureConsumePipeline(context);

                // Allow downstream services to append their specific configurations
                configureRabbitMq?.Invoke(context, cfg);
                
                // Automatically configure endpoints based on discovered consumers
                cfg.ConfigureEndpoints(context);
            });
        });

        return builder;
    }
}
