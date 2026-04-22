using AlbyOnContainers.Shared.Application.Infrastructure.Filters;
using MassTransit;

namespace AlbyOnContainers.Shared.Application.Infrastructure;

public static class SharedInfrastructureExtensions
{
    public static void ConfigurePimMediatorPipeline(this IMediatorRegistrationConfigurator configurator)
    {
        configurator.ConfigureMediator((context, cfg) =>
        {
            cfg.ConfigurePimConsumePipeline(context);
        });
    }

    public static void ConfigurePimConsumePipeline(this IConsumePipeConfigurator configurator, IRegistrationContext context)
    {
        configurator.UseConsumeFilter(typeof(ConsumeTelemetryFilter<>), context);
        configurator.UseConsumeFilter(typeof(GlobalExceptionFilter<>), context);
        configurator.UseConsumeFilter(typeof(ValidationFilter<>), context);
    }
}
