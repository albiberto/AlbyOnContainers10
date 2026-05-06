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

        public IKernelBuilder WithMessaging<TDbContext>(Action<IEntityFrameworkOutboxConfigurator> configureOutbox, string? section, params Type[] assemblyMarkers) where TDbContext : DbContext
        {
            ValidateMarkers(assemblyMarkers);
            builder.BindOptions(section);
            BuildAndConfigureMassTransit<TDbContext>(builder.Host.Services, assemblyMarkers, configureOutbox);

            return builder;
        }

        public IKernelBuilder WithMessaging<TDbContext>(Action<MessagingOptions> configureOptions, Action<IEntityFrameworkOutboxConfigurator> configureOutbox, params Type[] assemblyMarkers) where TDbContext : DbContext
        {
            ValidateMarkers(assemblyMarkers);
            builder.ConfigureOptions(configureOptions);
            BuildAndConfigureMassTransit<TDbContext>(builder.Host.Services, assemblyMarkers, configureOutbox);

            return builder;
        }

        public IKernelBuilder WithMessaging<TDbContext, TMarker>(Action<IEntityFrameworkOutboxConfigurator> configureOutbox, string? section = null) where TDbContext : DbContext =>
            builder.WithMessaging<TDbContext>(configureOutbox, section, typeof(TMarker));

        public IKernelBuilder WithMessaging<TDbContext, TMarker>(Action<MessagingOptions> configureOptions, Action<IEntityFrameworkOutboxConfigurator> configureOutbox) where TDbContext : DbContext =>
            builder.WithMessaging<TDbContext>(configureOptions, configureOutbox, typeof(TMarker));

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

    private static void BuildAndConfigureMassTransit<TDbContext>(IServiceCollection services, Type[] markers, Action<IEntityFrameworkOutboxConfigurator> configureOutbox) where TDbContext : DbContext
    {
        // 1. Register the plugin to inject Outbox tables into the EF Core ModelBuilder
        services.AddSingleton<IModelConfigurationPlugin, MassTransitOutboxPlugin>();

        // 2. Register the interceptor (resolved from the local Messaging module namespace)
        services.AddScoped<IInterceptor, DomainEventDispatcherInterceptor>();

        var assemblies = markers.Select(t => t.Assembly).Distinct().ToArray();

        // 3. IN-PROCESS MEDIATOR (Commands & Queries ONLY)
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

        // 4. OUT-OF-PROCESS BUS (Events & External Integration ONLY)
        services.AddMassTransit(cfg =>
        {
            cfg.AddConsumers(type => type.GetCustomAttribute<EventConsumerAttribute>() is not null, assemblies);
            cfg.SetKebabCaseEndpointNameFormatter();
            cfg.DisableUsageTelemetry();

            // The caller is responsible for selecting the DB provider (e.g. UsePostgres, UseSqlServer).
            // UseBusOutbox is enforced by the kernel to guarantee the outbox delivery pipeline is always active.
            cfg.AddEntityFrameworkOutbox<TDbContext>(o =>
            {
                configureOutbox(o);
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
                    rmq.UseMessageRetry(r => r.Exponential(
                        options.RetryCount,
                        options.RetryInitialInterval,
                        options.RetryMaxInterval,
                        options.RetryDeltaInterval));

                rmq.UseConsumeFilter(typeof(GlobalExceptionFilter<>), context);
                rmq.UseConsumeFilter(typeof(ValidationFilter<>), context);

                rmq.ConfigureEndpoints(context);
            });
        });

        services.AddValidatorsFromAssemblies(assemblies, includeInternalTypes: true);
    }
}