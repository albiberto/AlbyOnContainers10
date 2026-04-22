using AlbyOnContainers.Shared.Application.Infrastructure.Filters;
using MassTransit;

namespace AlbyOnContainers.Shared.Application.Infrastructure;

public static class SharedInfrastructureExtensions
{
    public static void ConfigureMediatorPipeline(this IMediatorRegistrationConfigurator configurator)
    {
        configurator.ConfigureMediator((context, cfg) =>
        {
            cfg.ConfigureConsumePipeline(context);
        });
    }

    public static void ConfigureConsumePipeline(this IConsumePipeConfigurator configurator, IRegistrationContext context)
    {
        // CRITICAL PIPELINE ORDER:
        // The sequence of these filters is strictly enforced. Outer filters wrap inner filters.

        // 1. Opens the logging Scope and injects TraceId, RequestId, etc. 
        // (All subsequent filters and the consumer will inherit this contextual metadata).
        configurator.UseConsumeFilter(typeof(ConsumeTelemetryFilter<>), context);
    
        // 2. Catches and logs exceptions. 
        // (Because it runs inside the Telemetry filter, its logs will automatically include the metadata from step 1).
        configurator.UseConsumeFilter(typeof(GlobalExceptionFilter<>), context);
    
        // 3. Validates the message before passing it to the actual Consumer. 
        // (Validation failure logs will also inherit the metadata from step 1).
        configurator.UseConsumeFilter(typeof(ValidationFilter<>), context);
    }
}