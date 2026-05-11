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

/// <summary>
///     Fluent extensions to register the messaging stack on the Kernel builder:
///     in-process Mediator (Commands/Queries marked with <see cref="MediatorConsumerAttribute" />),
///     out-of-process Bus (Events marked with <see cref="EventConsumerAttribute" />),
///     transactional Outbox via EF Core, and a default filter pipeline (Global exception logger + FluentValidation).
/// </summary>
public static class MessagingKernelExtensions
{
    // ==============================================================================
    // PUBLIC API (Fluent Builder)
    // ==============================================================================

    extension(IKernelBuilder builder)
    {
        /// <summary>Registers messaging binding <see cref="MessagingOptions" /> from configuration.</summary>
        public IKernelBuilder WithMessaging<TDbContext>(Action<IEntityFrameworkOutboxConfigurator> configureOutbox, string? section, params Type[] assemblyMarkers) where TDbContext : DbContext
        {
            ValidateMarkers(assemblyMarkers);
            builder.BindOptions(section);
            BuildAndConfigureMassTransit<TDbContext>(builder.Host.Services, assemblyMarkers, configureOutbox);

            return builder;
        }

        /// <summary>Registers messaging from a named connection string, parsing AMQP/AMQPS details inside the kernel.</summary>
        public IKernelBuilder WithMessaging<TDbContext>(string connectionStringName, Action<IEntityFrameworkOutboxConfigurator> configureOutbox, params Type[] assemblyMarkers) where TDbContext : DbContext
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(connectionStringName);
            ValidateMarkers(assemblyMarkers);

            builder.ConfigureOptions(opt => opt.ConnectionStringName = connectionStringName);
            BuildAndConfigureMassTransit<TDbContext>(builder.Host.Services, assemblyMarkers, configureOutbox);

            return builder;
        }

        /// <summary>Registers messaging configuring <see cref="MessagingOptions" /> via a lambda.</summary>
        public IKernelBuilder WithMessaging<TDbContext>(Action<MessagingOptions> configureOptions, Action<IEntityFrameworkOutboxConfigurator> configureOutbox, params Type[] assemblyMarkers) where TDbContext : DbContext
        {
            ValidateMarkers(assemblyMarkers);
            builder.ConfigureOptions(configureOptions);
            BuildAndConfigureMassTransit<TDbContext>(builder.Host.Services, assemblyMarkers, configureOutbox);

            return builder;
        }

        /// <summary>Single-marker shortcut over the params <c>WithMessaging</c> overload.</summary>
        public IKernelBuilder WithMessaging<TDbContext, TMarker>(Action<IEntityFrameworkOutboxConfigurator> configureOutbox, string? section = null) where TDbContext : DbContext =>
            builder.WithMessaging<TDbContext>(configureOutbox, section, new[] { typeof(TMarker) });

        /// <summary>Single-marker shortcut over the params <c>WithMessaging</c> overload.</summary>
        public IKernelBuilder WithMessaging<TDbContext, TMarker>(Action<MessagingOptions> configureOptions, Action<IEntityFrameworkOutboxConfigurator> configureOutbox) where TDbContext : DbContext =>
            builder.WithMessaging<TDbContext>(configureOptions, configureOutbox, new[] { typeof(TMarker) });

        /// <summary>Single-marker shortcut over the named connection string overload.</summary>
        public IKernelBuilder WithMessaging<TDbContext, TMarker>(string connectionStringName, Action<IEntityFrameworkOutboxConfigurator> configureOutbox) where TDbContext : DbContext =>
            builder.WithMessaging<TDbContext>(connectionStringName, configureOutbox, new[] { typeof(TMarker) });

        /// <summary>
        ///     Registers an additional open-generic consume filter (e.g. <c>typeof(MyFilter&lt;&gt;)</c>) that the
        ///     kernel will apply to both the mediator and bus pipelines, after the mandatory Global/Validation filters.
        ///     Order between this call and <c>WithMessaging</c> is irrelevant — the registry is read lazily at
        ///     MassTransit boot time.
        /// </summary>
        public IKernelBuilder AddMessagingFilter(Type openGenericFilter)
        {
            ArgumentNullException.ThrowIfNull(openGenericFilter);
            if (!openGenericFilter.IsGenericTypeDefinition)
                throw new ArgumentException(
                    $"Filter '{openGenericFilter.FullName}' must be an open generic type, e.g. typeof(MyFilter<>).",
                    nameof(openGenericFilter));

            var registry = builder.GetOrAddFilterRegistry();
            registry.Add(openGenericFilter);

            // Make the filter resolvable at message-consumption time via DI.
            builder.Host.Services.AddScoped(openGenericFilter);

            return builder;
        }

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

        private MessagingFilterRegistry GetOrAddFilterRegistry()
        {
            var existing = builder.Host.Services
                .FirstOrDefault(s => s.ServiceType == typeof(MessagingFilterRegistry))
                ?.ImplementationInstance as MessagingFilterRegistry;

            if (existing is not null) return existing;

            var registry = new MessagingFilterRegistry();
            builder.Host.Services.AddSingleton(registry);
            return registry;
        }
    }

    // ==============================================================================
    // PRIVATE STATIC HELPERS & LOGIC
    // ==============================================================================

    private static void ValidateMarkers(Type[] markers)
    {
        ArgumentNullException.ThrowIfNull(markers);

        if (markers.Length == 0) throw new ArgumentException("At least one marker type must be provided to scan for consumers.", nameof(markers));
    }

    private static MessagingFilterRegistry GetOrAddFilterRegistry(IServiceCollection services)
    {
        var existing = services
            .FirstOrDefault(s => s.ServiceType == typeof(MessagingFilterRegistry))
            ?.ImplementationInstance as MessagingFilterRegistry;

        if (existing is not null) return existing;

        var registry = new MessagingFilterRegistry();
        services.AddSingleton(registry);
        return registry;
    }

    private static void BuildAndConfigureMassTransit<TDbContext>(IServiceCollection services, Type[] markers, Action<IEntityFrameworkOutboxConfigurator> configureOutbox) where TDbContext : DbContext
    {
        // Make sure the registry exists even if AddMessagingFilter was never called.
        GetOrAddFilterRegistry(services);

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

                var registry = context.GetRequiredService<MessagingFilterRegistry>();
                foreach (var filter in registry.Filters)
                    mCfg.UseConsumeFilter(filter, context);
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
                var connection = RabbitMqConnection.Resolve(context, options);

                rmq.Host(connection.Host, (ushort)connection.Port, connection.VirtualHost, h =>
                {
                    h.Username(connection.Username);
                    h.Password(connection.Password);

                    if (connection.UseSsl)
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

                var registry = context.GetRequiredService<MessagingFilterRegistry>();
                foreach (var filter in registry.Filters)
                    rmq.UseConsumeFilter(filter, context);

                rmq.ConfigureEndpoints(context);
            });
        });

        services.AddValidatorsFromAssemblies(assemblies, includeInternalTypes: true);
    }
}
