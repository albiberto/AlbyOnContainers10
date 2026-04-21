using AlbyOnContainers.Shared.Application.Infrastructure.Filters;
using MassTransit;

namespace AlbyOnContainers.Shared.Application.Infrastructure;

public static class SharedInfrastructureExtensions
{
    public static void ConfigureMediator(this IMediatorRegistrationConfigurator configurator)
    {
        configurator.ConfigureMediator((context, cfg) =>
        {
            // 1. OUTER LAYER: Exception Handling and Telemetry
            cfg.UseConsumeFilter(typeof(GlobalExceptionFilter<>), context);

            // 2. INNER LAYER: Command Validation
            cfg.UseConsumeFilter(typeof(ValidationFilter<>), context);
        });
    }
}