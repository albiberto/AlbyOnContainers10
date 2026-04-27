namespace AlbyOnContainers.Kernel.Messaging;

using Filters;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Options;

public static class MessagingKernelExtensions
{
    // ==============================================================================
    // PUBLIC API
    // ==============================================================================

    extension(IKernelBuilder builder)
    {
        public IKernelBuilder WithMessaging<TMarker>(string? section = null)
        {
            builder.BindOptions(section);
            builder.ConfigureMassTransit<TMarker>();
            return builder;
        }

        public IKernelBuilder WithMessaging<TMarker>(Action<MessagingOptions> configureOptions)
        {
            builder.ConfigureOptions(configureOptions);
            builder.ConfigureMassTransit<TMarker>();
            return builder;
        }

        public IKernelBuilder WithMessaging<TMarker, TDbContext>(string? section = null) where TDbContext : DbContext
        {
            builder.BindOptions(section);
            builder.ConfigureMassTransitWithOutbox<TMarker, TDbContext>();
            return builder;
        }

        public IKernelBuilder WithMessaging<TMarker, TDbContext>(Action<MessagingOptions> configureOptions) where TDbContext : DbContext
        {
            builder.ConfigureOptions(configureOptions);
            builder.ConfigureMassTransitWithOutbox<TMarker, TDbContext>();
            return builder;
        }
        
        // ==============================================================================
        // PRIVATE BOILERPLATE HELPERS
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

        private void ConfigureMassTransitWithOutbox<TMarker, TDbContext>() where TDbContext : DbContext
        {
            builder.ConfigureMassTransit<TMarker>(x =>
            {
                x.AddEntityFrameworkOutbox<TDbContext>(o =>
                {
                    o.UsePostgres();
                    o.UseBusOutbox();
                });
            });
        }

        private void ConfigureMassTransit<TMarker>(Action<IBusRegistrationConfigurator>? configureBus = null)
        {
            var services = builder.Host.Services;
            var marker = typeof(TMarker).Assembly;

            services.AddMassTransit(x =>
            {
                x.SetKebabCaseEndpointNameFormatter();
                x.DisableUsageTelemetry();
                x.AddConsumers(marker);

                // Apply optional configurations (like the Outbox)
                configureBus?.Invoke(x);

                x.UsingRabbitMq((context, cfg) =>
                {
                    var options = context.GetRequiredService<IOptions<MessagingOptions>>().Value;
                    cfg.Host(options.ConnectionString);

                    cfg.UseConsumeFilter(typeof(GlobalExceptionFilter<>), context);
                    cfg.UseConsumeFilter(typeof(ValidationFilter<>), context);

                    cfg.ConfigureEndpoints(context);
                });
            });

            // Mediator for internal CQRS
            services.AddMediator(cfg =>
            {
                cfg.AddConsumers(marker);

                cfg.ConfigureMediator((context, configure) =>
                {
                    configure.UseConsumeFilter(typeof(GlobalExceptionFilter<>), context);
                    configure.UseConsumeFilter(typeof(ValidationFilter<>), context);
                });
            });
        }
    }
}