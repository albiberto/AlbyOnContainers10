using System.Reflection;
using AlbyOnContainers.Kernel.Messaging.Attributes;
using AlbyOnContainers.Kernel.Messaging.Filters;
using AlbyOnContainers.Kernel.Messaging.Options;
using FluentValidation;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AlbyOnContainers.Kernel.Messaging;

using Interceptors;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Persistence.Abstractions;
using PlugIns;

public static class MessagingKernelExtensions
{
    // ==============================================================================
    // PUBLIC API (Fluent Builder)
    // ==============================================================================

    extension (IKernelBuilder builder)
    {
        // ==============================================================================
        // OVERLOADS (Multi-Assembly Support with Mandatory Outbox Pattern)
        // ==============================================================================

        public IKernelBuilder WithMessaging<TDbContext>(string? section, params Type[] assemblyMarkers) where TDbContext : DbContext
        {
            ValidateMarkers(assemblyMarkers);
            builder.BindOptions(section);
            BuildAndConfigureMassTransit<TDbContext>(builder.Host.Services, assemblyMarkers);
            
            return builder;
        }

        public IKernelBuilder WithMessaging<TDbContext>(Action<MessagingOptions> configureOptions, params Type[] assemblyMarkers) where TDbContext : DbContext
        {
            ValidateMarkers(assemblyMarkers);
            builder.ConfigureOptions(configureOptions);
            BuildAndConfigureMassTransit<TDbContext>(builder.Host.Services, assemblyMarkers);
            
            return builder;
        }

        public IKernelBuilder WithMessaging<TDbContext, TMarker>(string? section = null) where TDbContext : DbContext => 
            builder.WithMessaging<TDbContext>(section, typeof(TMarker));

        public IKernelBuilder WithMessaging<TDbContext, TMarker>(Action<MessagingOptions> configureOptions) where TDbContext : DbContext =>
            builder.WithMessaging<TDbContext>(configureOptions, typeof(TMarker));

        // ==============================================================================
        // INTERNAL OPTIONS HELPERS
        // ==============================================================================

        private void BindOptions(string? section) =>
            builder.Host.Services
                .AddOptions<MessagingOptions>()
                .BindConfiguration(section ?? MessagingOptions.Section)
                .ValidateDataAnnotations()
                .ValidateOnStart();

        private void ConfigureOptions(Action<MessagingOptions> configure) =>
            builder.Host.Services
                .AddOptions<MessagingOptions>()
                .Configure(configure)
                .ValidateDataAnnotations()
                .ValidateOnStart();
    }

    // ==============================================================================
    // PRIVATE STATIC HELPERS & LOGIC
    // ==============================================================================

    private static void ValidateMarkers(Type[] markers)
    {
        ArgumentNullException.ThrowIfNull(markers);

        if (markers.Length == 0) throw new ArgumentException("At least one marker type must be provided to scan for consumers.", nameof(markers));
    }

    private static void BuildAndConfigureMassTransit<TDbContext>(IServiceCollection services, Type[] markers) where TDbContext : DbContext
    {
        // 1. Registra il plugin per iniettare le tabelle Outbox nel ModelBuilder di EF Core
        services.AddSingleton<IModelConfigurationPlugin, MassTransitOutboxPlugin>();
        
        // 2. Registra l'interceptor (ora risolto dal namespace locale del modulo Messaging)
        services.AddScoped<IInterceptor, DomainEventDispatcherInterceptor>();

        var assemblies = markers.Select(t => t.Assembly).Distinct().ToArray();

        // 2. IN-PROCESS MEDIATOR (Commands & Queries ONLY)
        services.AddMediator(cfg =>
        {
            cfg.AddConsumers(
                type => type.GetCustomAttribute<MediatorConsumerAttribute>() is not null,
                assemblies);

            cfg.ConfigureMediator((context, mCfg) =>
            {
                mCfg.UseConsumeFilter(typeof(GlobalExceptionFilter<>), context);
                mCfg.UseConsumeFilter(typeof(ValidationFilter<>), context);
            });
        });

        // 3. OUT-OF-PROCESS BUS (Events & External Integration ONLY)
        services.AddMassTransit(cfg =>
        {
            cfg.AddConsumers(type => type.GetCustomAttribute<EventConsumerAttribute>() is not null, assemblies);
            cfg.SetKebabCaseEndpointNameFormatter();
            cfg.DisableUsageTelemetry();

            cfg.AddEntityFrameworkOutbox<TDbContext>(o =>
            {
                o.UsePostgres();
                o.UseBusOutbox();
            });

            cfg.UsingRabbitMq((context, rmq) =>
            {
                var options = context.GetRequiredService<IOptions<MessagingOptions>>().Value;

                rmq.Host(options.Host, (ushort)options.Port, "/", h =>
                {
                    h.Username(options.Username);
                    h.Password(options.Password);

                    if (options.UseSsl)
                        h.UseSsl(_ => { });
                });

                // GUARD: MassTransit's UseMessageRetry throws if RetryCount == 0.
                if (options.RetryCount > 0)
                {
                    rmq.UseMessageRetry(r => r.Exponential(
                        options.RetryCount,
                        options.RetryInitialInterval,
                        options.RetryMaxInterval,
                        options.RetryDeltaInterval
                    ));
                }

                rmq.UseConsumeFilter(typeof(GlobalExceptionFilter<>), context);
                rmq.UseConsumeFilter(typeof(ValidationFilter<>), context);

                rmq.ConfigureEndpoints(context);
            });
        });

        services.AddValidatorsFromAssemblies(assemblies, includeInternalTypes: true);
    }
}