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
        // 1. OVERLOAD CON OUTBOX PATTERN (Per microservizi con DbContext)
        // ==============================================================================

        public IKernelBuilder WithMessaging<TMarker, TDbContext>(string? section = null)
            where TDbContext : DbContext
        {
            builder.BindOptions(section);
            BuildAndConfigureMassTransit<TMarker, TDbContext>(builder.Host.Services);
            return builder;
        }

        public IKernelBuilder WithMessaging<TMarker, TDbContext>(Action<MessagingOptions> configureOptions)
            where TDbContext : DbContext
        {
            builder.ConfigureOptions(configureOptions);
            BuildAndConfigureMassTransit<TMarker, TDbContext>(builder.Host.Services);
            return builder;
        }

        // ==============================================================================
        // 2. OVERLOAD SENZA OUTBOX (Per microservizi stateless senza DB)
        // ==============================================================================

        public IKernelBuilder WithMessaging<TMarker>(string? section = null)
        {
            builder.BindOptions(section);
            BuildAndConfigureMassTransit<TMarker>(builder.Host.Services);
            return builder;
        }

        public IKernelBuilder WithMessaging<TMarker>(Action<MessagingOptions> configureOptions)
        {
            builder.ConfigureOptions(configureOptions);
            BuildAndConfigureMassTransit<TMarker>(builder.Host.Services);
            return builder;
        }

        // ==============================================================================
        // PRIVATE HELPERS E LOGICA DI REGISTRAZIONE
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

        // Configura il MassTransit includendo l'Outbox Pattern di Entity Framework
        private static void BuildAndConfigureMassTransit<TMarker, TDbContext>(IServiceCollection services)
            where TDbContext : DbContext
        {
            BuildAndConfigureMassTransit<TMarker>(services, cfg =>
            {
                cfg.AddEntityFrameworkOutbox<TDbContext>(o =>
                {
                    o.UsePostgres();
                    o.UseBusOutbox();
                });
            });
        }

        // Configurazione core: isola il Mediator (CQRS In-Process) da RabbitMQ (Eventi Asincroni)
        private static void BuildAndConfigureMassTransit<TMarker>(
            IServiceCollection services, 
            Action<IBusRegistrationConfigurator>? configureBus = null)
        {
            var assembly = typeof(TMarker).Assembly;

            // 1. IN-PROCESS MEDIATOR (Commands & Queries ONLY)
            services.AddMediator(cfg =>
            {
                cfg.AddConsumers(type => 
                    type.Name.EndsWith("CommandConsumer") || type.Name.EndsWith("QueryConsumer"), 
                    assembly);

                cfg.ConfigureMediator((context, mCfg) =>
                {
                    mCfg.UseConsumeFilter(typeof(ValidationFilter<>), context);
                    mCfg.UseConsumeFilter(typeof(GlobalExceptionFilter<>), context);
                });
            });

            // 2. OUT-OF-PROCESS BUS (Events & External Integration ONLY)
            services.AddMassTransit(cfg =>
            {
                cfg.AddConsumers(type => 
                    type.Name.EndsWith("EventConsumer"), 
                    assembly);

                cfg.SetKebabCaseEndpointNameFormatter();

                // Applica configurazioni custom al Bus (es. l'Outbox Pattern iniettato dall'overload)
                configureBus?.Invoke(cfg);

                cfg.UsingRabbitMq((context, rmq) =>
                {
                    var options = context.GetRequiredService<IOptions<MessagingOptions>>().Value;

                    rmq.Host(options.Host, h =>
                    {
                        h.Username(options.Username);
                        h.Password(options.Password);
                    });

                    // Resilienza: Exponential Backoff per errori transitori
                    rmq.UseMessageRetry(r => r.Exponential(
                        options.RetryCount,
                        TimeSpan.FromSeconds(options.RetryInitialIntervalSeconds),
                        TimeSpan.FromSeconds(options.RetryMaxIntervalSeconds),
                        TimeSpan.FromSeconds(5)
                    ));

                    rmq.UseConsumeFilter(typeof(ValidationFilter<>), context);
                    rmq.UseConsumeFilter(typeof(GlobalExceptionFilter<>), context);

                    rmq.ConfigureEndpoints(context);
                });
            });

            // Registra i Validator per FluentValidation
            services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);
        }
    }
}