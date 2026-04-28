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

public static class MessagingKernelExtensions
{
    // ==============================================================================
    // PUBLIC API (Fluent Builder)
    // ==============================================================================

    extension (IKernelBuilder builder)
    {
        // ==============================================================================
        // 1. OVERLOADS CON OUTBOX PATTERN (Multi-Assembly Support)
        // ==============================================================================

        // --- BASE IMPLEMENTATION (PARAMS ARRAY) ---

        public IKernelBuilder WithMessaging<TDbContext>(Action<IEntityFrameworkOutboxConfigurator> configureOutbox, 
            string? section, 
            params Type[] assemblyMarkers)
            where TDbContext : DbContext
        {
            ValidateMarkers(assemblyMarkers);
            builder.BindOptions(section);
            BuildAndConfigureMassTransit<TDbContext>(builder.Host.Services, assemblyMarkers, configureOutbox);
            return builder;
        }

        public IKernelBuilder WithMessaging<TDbContext>(
            Action<MessagingOptions> configureOptions, 
            Action<IEntityFrameworkOutboxConfigurator> configureOutbox, 
            params Type[] assemblyMarkers)
            where TDbContext : DbContext
        {
            ValidateMarkers(assemblyMarkers);
            builder.ConfigureOptions(configureOptions);
            BuildAndConfigureMassTransit<TDbContext>(builder.Host.Services, assemblyMarkers, configureOutbox);
            return builder;
        }

        // --- SINGLE MARKER SYNTACTIC SUGAR (<TDbContext, TMarker>) ---

        public IKernelBuilder WithMessaging<TDbContext, TMarker>(
            Action<IEntityFrameworkOutboxConfigurator> configureOutbox, 
            string? section = null)
            where TDbContext : DbContext
        {
            return builder.WithMessaging<TDbContext>(configureOutbox, section, [typeof(TMarker)]);
        }

        public IKernelBuilder WithMessaging<TDbContext, TMarker>(
            Action<MessagingOptions> configureOptions, 
            Action<IEntityFrameworkOutboxConfigurator> configureOutbox)
            where TDbContext : DbContext
        {
            return builder.WithMessaging<TDbContext>(configureOptions, configureOutbox, [typeof(TMarker)]);
        }

        // ==============================================================================
        // 2. OVERLOADS SENZA OUTBOX (Multi-Assembly Support)
        // ==============================================================================

        // --- BASE IMPLEMENTATION (PARAMS ARRAY) ---

        public IKernelBuilder WithMessaging(string? section, params Type[] assemblyMarkers)
        {
            ValidateMarkers(assemblyMarkers);
            builder.BindOptions(section);
            BuildAndConfigureMassTransit(builder.Host.Services, assemblyMarkers);
            return builder;
        }

        public IKernelBuilder WithMessaging(Action<MessagingOptions> configureOptions, params Type[] assemblyMarkers)
        {
            ValidateMarkers(assemblyMarkers);
            builder.ConfigureOptions(configureOptions);
            BuildAndConfigureMassTransit(builder.Host.Services, assemblyMarkers);
            return builder;
        }

        // --- SINGLE MARKER SYNTACTIC SUGAR (<TMarker>) ---

        public IKernelBuilder WithMessaging<TMarker>(string? section = null)
        {
            return builder.WithMessaging(section, [typeof(TMarker)]);
        }

        public IKernelBuilder WithMessaging<TMarker>(Action<MessagingOptions> configureOptions)
        {
            return builder.WithMessaging(configureOptions, [typeof(TMarker)]);
        }

        // ==============================================================================
        // INTERNAL OPTIONS HELPERS
        // ==============================================================================

        private void BindOptions(string? section)
        {
            builder.Host.Services
                .AddOptions<MessagingOptions>()
                .BindConfiguration(section ?? MessagingOptions.Section)
                .ValidateDataAnnotations()
                .ValidateOnStart();
        }

        private void ConfigureOptions(Action<MessagingOptions> configure)
        {
            builder.Host.Services
                .AddOptions<MessagingOptions>()
                .Configure(configure)
                .ValidateDataAnnotations()
                .ValidateOnStart();
        }
    }

    // ==============================================================================
    // PRIVATE STATIC HELPERS & LOGIC
    // ==============================================================================

    private static void ValidateMarkers(Type[] markers)
    {
        ArgumentNullException.ThrowIfNull(markers);
        
        if (markers.Length == 0)
        {
            throw new ArgumentException("At least one marker type must be provided to scan for consumers.", nameof(markers));
        }
    }

    private static void BuildAndConfigureMassTransit<TDbContext>(
        IServiceCollection services, 
        Type[] markers, 
        Action<IEntityFrameworkOutboxConfigurator> configureOutbox)
        where TDbContext : DbContext
    {
        // ARCHITECTURAL NOTE: The caller is now responsible for calling 
        // 'o.UseBusOutbox()' manually if they want the outbox to publish 
        // to the message broker automatically.
        BuildAndConfigureMassTransit(services, markers, cfg => cfg.AddEntityFrameworkOutbox<TDbContext>(configureOutbox));
    }

    private static void BuildAndConfigureMassTransit(
        IServiceCollection services,
        Type[] markers,
        Action<IBusRegistrationConfigurator>? configureBus = null)
    {
        var assemblies = markers.Select(t => t.Assembly).Distinct().ToArray();

        // 1. IN-PROCESS MEDIATOR (Commands & Queries ONLY)
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

        // 2. OUT-OF-PROCESS BUS (Events & External Integration ONLY)
        services.AddMassTransit(cfg =>
        {
            cfg.AddConsumers(
                type => type.GetCustomAttribute<EventConsumerAttribute>() is not null,
                assemblies);

            cfg.SetKebabCaseEndpointNameFormatter();
            cfg.DisableUsageTelemetry();

            configureBus?.Invoke(cfg);

            cfg.UsingRabbitMq((context, rmq) =>
            {
                var options = context.GetRequiredService<IOptions<MessagingOptions>>().Value;

                rmq.Host(options.Host, h =>
                {
                    h.Username(options.Username);
                    h.Password(options.Password);
                });

                rmq.UseMessageRetry(r => r.Exponential(
                    options.RetryCount,
                    options.RetryInitialInterval,
                    options.RetryMaxInterval,
                    options.RetryDeltaInterval
                ));

                rmq.UseConsumeFilter(typeof(GlobalExceptionFilter<>), context);
                rmq.UseConsumeFilter(typeof(ValidationFilter<>), context);

                rmq.ConfigureEndpoints(context);
            });
        });

        services.AddValidatorsFromAssemblies(assemblies, includeInternalTypes: true);
    }
}