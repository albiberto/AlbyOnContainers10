using System;
using System.Reflection;
using AlbyOnContainers.Kernel.Abstraction;
using AlbyOnContainers.Kernel.Messaging.Filters;
using AlbyOnContainers.Kernel.Messaging.Options;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AlbyOnContainers.Kernel.Messaging;

public static class MessagingKernelExtensions
{
    public static IKernelBuilder WithMessaging(this IKernelBuilder builder, string sectionName = MessagingOptions.SectionName)
    {
        builder.Host.Services.AddOptions<MessagingOptions>()
            .BindConfiguration(sectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.AddInternalMessaging(typeof(MessagingKernelExtensions).Assembly);
        return builder;
    }

    public static IKernelBuilder WithMessaging(this IKernelBuilder builder, Action<MessagingOptions> configureOptions)
    {
        builder.Host.Services.AddOptions<MessagingOptions>()
            .Configure(configureOptions)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.AddInternalMessaging(typeof(MessagingKernelExtensions).Assembly);
        return builder;
    }

    public static IKernelBuilder WithMessaging<TMarker>(this IKernelBuilder builder, string sectionName = MessagingOptions.SectionName)
    {
        builder.Host.Services.AddOptions<MessagingOptions>()
            .BindConfiguration(sectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.AddInternalMessaging(typeof(TMarker).Assembly);
        return builder;
    }

    private static void AddInternalMessaging(this IKernelBuilder builder, Assembly scanAssembly)
    {
        builder.Host.Services.AddMassTransit(x =>
        {
            x.SetKebabCaseEndpointNameFormatter();
            x.DisableUsageTelemetry();

            x.AddConsumers(scanAssembly);

            x.UsingRabbitMq((context, cfg) =>
            {
                var options = context.GetRequiredService<IOptions<MessagingOptions>>().Value;
                cfg.Host(options.ConnectionString);

                cfg.UseConsumeFilter(typeof(ConsumeTelemetryFilter<>), context);
                cfg.UseConsumeFilter(typeof(GlobalExceptionFilter<>), context);
                cfg.UseConsumeFilter(typeof(ValidationFilter<>), context);

                cfg.ConfigureEndpoints(context);
            });
        });

        // Add mediator by default to support CQRS 
        builder.Host.Services.AddMediator(cfg =>
        {
            cfg.AddConsumers(scanAssembly);

            cfg.ConfigureMediator((context, mcfg) =>
            {
                mcfg.UseConsumeFilter(typeof(ConsumeTelemetryFilter<>), context);
                mcfg.UseConsumeFilter(typeof(GlobalExceptionFilter<>), context);
                mcfg.UseConsumeFilter(typeof(ValidationFilter<>), context);
            });
        });
    }

    // Methods extending something other than IKernelBuilder do not violate the rule
    public static void AddAlbyOutbox<TDbContext>(this IBusRegistrationConfigurator x, Action<IEntityFrameworkOutboxConfigurator>? configureOutbox = null) 
        where TDbContext : DbContext
    {
        x.AddEntityFrameworkOutbox<TDbContext>(o =>
        {
            configureOutbox?.Invoke(o);
            o.UseBusOutbox();
        });
    }
}
