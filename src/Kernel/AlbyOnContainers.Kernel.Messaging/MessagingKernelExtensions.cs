using System;
using AlbyOnContainers.Kernel.Abstraction;
using AlbyOnContainers.Kernel.Messaging.Filters;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AlbyOnContainers.Kernel.Messaging;

public static class MessagingKernelExtensions
{
    public static IKernelBuilder WithMessaging(this IKernelBuilder builder, Action<IBusRegistrationConfigurator>? configureBus = null)
    {
        var connectionString = builder.Host.Configuration.GetConnectionString("messaging");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Fail-Fast: Connection string 'messaging' not found. Distributed messaging cannot be established.");
        }

        builder.Host.Services.AddMassTransit(x =>
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

                var filterOptions = context.GetService<Microsoft.Extensions.Options.IOptions<MessagingFilterOptions>>()?.Value;
                if (filterOptions != null)
                {
                    foreach (var filterType in filterOptions.FilterTypes)
                    {
                        cfg.UseConsumeFilter(filterType, context);
                    }
                }

                cfg.ConfigureEndpoints(context);
            });
        });

        return builder;
    }

    public static IKernelBuilder WithMediator(this IKernelBuilder builder, Action<IMediatorRegistrationConfigurator> configureConsumers)
    {
        builder.Host.Services.AddMediator(cfg =>
        {
            configureConsumers(cfg);

            cfg.ConfigureMediator((context, mcfg) =>
            {
                mcfg.UseConsumeFilter(typeof(ConsumeTelemetryFilter<>), context);
                mcfg.UseConsumeFilter(typeof(GlobalExceptionFilter<>), context);
                mcfg.UseConsumeFilter(typeof(ValidationFilter<>), context);

                var filterOptions = context.GetService<Microsoft.Extensions.Options.IOptions<MessagingFilterOptions>>()?.Value;
                if (filterOptions != null)
                {
                    foreach (var filterType in filterOptions.FilterTypes)
                    {
                        mcfg.UseConsumeFilter(filterType, context);
                    }
                }
            });
        });

        return builder;
    }

    public static IKernelBuilder WithEfCoreOutbox<TDbContext>(this IKernelBuilder builder, Action<IEntityFrameworkOutboxConfigurator>? configureOutbox = null) 
        where TDbContext : DbContext
    {
        builder.Host.Services.AddMassTransit(x =>
        {
            x.AddEntityFrameworkOutbox<TDbContext>(o =>
            {
                configureOutbox?.Invoke(o);
                o.UseBusOutbox();
            });
        });

        return builder;
    }

    public static IKernelBuilder AddMessagingFilter<TFilter>(this IKernelBuilder builder)
        where TFilter : class, IFilter<ConsumeContext>
    {
        // To allow appending filters globally, we can use an IConfigurationObserver or just rely on a startup filter.
        // Wait, MassTransit allows registering global filters via AddPipeSpecificationObserver
        // However, a simpler way in MassTransit v8+ is to register the filter in DI, and then when calling UsingRabbitMq or ConfigureMediator, use it.
        // Since we are creating a Fluent API, it's better to store custom filters in the service collection as a list of types.
        builder.Host.Services.Configure<MessagingFilterOptions>(options =>
        {
            options.FilterTypes.Add(typeof(TFilter));
        });

        return builder;
    }
}

public class MessagingFilterOptions
{
    public System.Collections.Generic.List<Type> FilterTypes { get; } = new();
}
