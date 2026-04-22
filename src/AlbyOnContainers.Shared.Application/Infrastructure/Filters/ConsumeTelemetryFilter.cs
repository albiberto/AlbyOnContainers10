using System.Diagnostics;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace AlbyOnContainers.Shared.Application.Infrastructure.Filters;

public class ConsumeTelemetryFilter<T>(ILogger<ConsumeTelemetryFilter<T>> logger) : IFilter<ConsumeContext<T>> where T : class
{
    public async Task Send(ConsumeContext<T> context, IPipe<ConsumeContext<T>> next)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["message_type"] = typeof(T).Name,
            ["message_id"] = context.MessageId,
            ["correlation_id"] = context.CorrelationId,
            ["conversation_id"] = context.ConversationId,
            ["request_id"] = context.RequestId,
            ["input_address"] = context.ReceiveContext.InputAddress?.ToString()
        });

        var stopwatch = Stopwatch.StartNew();

        await next.Send(context);

        stopwatch.Stop();

        var logLevel = IsQueryMessage() ? LogLevel.Debug : LogLevel.Information;
        logger.Log(
            logLevel,
            "PIM MassTransit consume completed {MessageType} in {ElapsedMs} ms",
            typeof(T).Name,
            stopwatch.Elapsed.TotalMilliseconds);
    }

    public void Probe(ProbeContext context) => context.CreateFilterScope("consume-telemetry");

    private static bool IsQueryMessage()
    {
        var name = typeof(T).Name;
        return name.StartsWith("Get", StringComparison.Ordinal) || name.StartsWith("Search", StringComparison.Ordinal);
    }
}
