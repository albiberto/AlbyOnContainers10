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
    extension (IKernelBuilder builder)
    {
        // ==============================================================================
        // 1. OVERLOAD CON OUTBOX PATTERN (Multi-Assembly Support)
        // ==============================================================================

        public IKernelBuilder WithMessaging<TDbContext>(string? section, params Type[] assemblyMarkers) where TDbContext : DbContext
        {
            if (assemblyMarkers.Length == 0) 
                throw new ArgumentException("At least one assembly marker must be provided.", nameof(assemblyMarkers));
            
            builder.BindOptions(section);
            BuildAndConfigureMassTransit<TDbContext>(builder.Host.Services, assemblyMarkers);
            
            return builder;
        }

        public IKernelBuilder WithMessaging<TDbContext>(Action<MessagingOptions> configureOptions, params Type[] assemblyMarkers) where TDbContext : DbContext
        {
            if (assemblyMarkers.Length == 0) 
                throw new ArgumentException("At least one assembly marker must be provided.", nameof(assemblyMarkers));

            builder.ConfigureOptions(configureOptions);
            BuildAndConfigureMassTransit<TDbContext>(builder.Host.Services, assemblyMarkers);
            
            return builder;
        }

        // ==============================================================================
        // 2. OVERLOAD SENZA OUTBOX (Multi-Assembly Support)
        // ==============================================================================

        public IKernelBuilder WithMessaging(string? section, params Type[] assemblyMarkers)
        {
            if (assemblyMarkers.Length == 0) 
                throw new ArgumentException("At least one assembly marker must be provided.", nameof(assemblyMarkers));

            builder.BindOptions(section);
            BuildAndConfigureMassTransit(builder.Host.Services, assemblyMarkers);
            
            return builder;
        }

        public IKernelBuilder WithMessaging(Action<MessagingOptions> configureOptions, params Type[] assemblyMarkers)
        {
            if (assemblyMarkers.Length == 0) 
                throw new ArgumentException("At least one assembly marker must be provided.", nameof(assemblyMarkers));

            builder.ConfigureOptions(configureOptions);
            BuildAndConfigureMassTransit(builder.Host.Services, assemblyMarkers);
            
            return builder;
        }

        // ==============================================================================
        // PRIVATE HELPERS & LOGIC
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

        private static void BuildAndConfigureMassTransit<TDbContext>(IServiceCollection services, Type[] markers) where TDbContext : DbContext =>
            BuildAndConfigureMassTransit(services, markers, cfg =>
            {
                cfg.AddEntityFrameworkOutbox<TDbContext>(o =>
                {
                    o.UsePostgres();
                    o.UseBusOutbox();
                });
            });

        private static void BuildAndConfigureMassTransit(IServiceCollection services, Type[] markers, Action<IBusRegistrationConfigurator>? configureBus = null)
        {
            // Extract unique assemblies to avoid scanning the same assembly multiple times
            var assemblies = markers.Select(t => t.Assembly).Distinct().ToArray();

            // 1. IN-PROCESS MEDIATOR (Commands & Queries ONLY)
            services.AddMediator(cfg =>
            {
                // Strict isolation: Only load consumers decorated with [MediatorConsumer]
                cfg.AddConsumers(type => 
                    type.GetCustomAttribute<MediatorConsumerAttribute>() is not null, 
                    assemblies);

                cfg.ConfigureMediator((context, mCfg) =>
                {
                    // LIFO Pipeline: GlobalExceptionFilter MUST be registered first to catch Validation exceptions
                    mCfg.UseConsumeFilter(typeof(GlobalExceptionFilter<>), context);
                    mCfg.UseConsumeFilter(typeof(ValidationFilter<>), context);
                });
            });

            // 2. OUT-OF-PROCESS BUS (Events & External Integration ONLY)
            services.AddMassTransit(cfg =>
            {
                // Strict isolation: Only load consumers decorated with [EventConsumer]
                cfg.AddConsumers(type => 
                    type.GetCustomAttribute<EventConsumerAttribute>() is not null, 
                    assemblies);

                cfg.SetKebabCaseEndpointNameFormatter();
                
                // Keep diagnostics clean in production environments
                cfg.DisableUsageTelemetry();

                // Apply custom bus configurations (like the EF Core Outbox)
                configureBus?.Invoke(cfg);

                cfg.UsingRabbitMq((context, rmq) =>
                {
                    var options = context.GetRequiredService<IOptions<MessagingOptions>>().Value;

                    rmq.Host(options.Host, h =>
                    {
                        h.Username(options.Username);
                        h.Password(options.Password);
                    });

                    // Transparent Resilience using TimeSpans
                    rmq.UseMessageRetry(r => r.Exponential(
                        options.RetryCount,
                        options.RetryInitialInterval,
                        options.RetryMaxInterval,
                        TimeSpan.FromSeconds(5) // delta
                    ));

                    // LIFO Pipeline: Consistent with Mediator behavior
                    rmq.UseConsumeFilter(typeof(GlobalExceptionFilter<>), context);
                    rmq.UseConsumeFilter(typeof(ValidationFilter<>), context);

                    rmq.ConfigureEndpoints(context);
                });
            });

            // Register FluentValidation across all scanned assemblies
            services.AddValidatorsFromAssemblies(assemblies, includeInternalTypes: true);
        }
    }
}