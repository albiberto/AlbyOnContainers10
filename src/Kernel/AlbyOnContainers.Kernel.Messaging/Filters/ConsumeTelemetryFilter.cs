using System.Diagnostics;
using AlbyOnContainers.Kernel.Messaging.Workers;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace AlbyOnContainers.Kernel.Messaging.Filters;

public class ConsumeTelemetryFilter<T>(ILogger<ConsumeTelemetryFilter<T>> logger) : IFilter<ConsumeContext<T>> where T : class
{
    public async Task Send(ConsumeContext<T> context, IPipe<ConsumeContext<T>> next)
    {
        var metadata = new Dictionary<string, object?>
        {
            ["MessageType"] = typeof(T).Name,
            ["MessageId"] = context.MessageId,
            ["TraceId"] = $"{Activity.Current?.TraceId}",
            ["CorrelationId"] = context.CorrelationId,
            ["RequestId"] = context.RequestId,
            ["ConversationId"] = context.ConversationId,
            ["InputAddress"] = $"{context.ReceiveContext.InputAddress}"
        };

        using var scope = logger.BeginScope(metadata);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await next.Send(context);
        }
        finally
        {
            stopwatch.Stop();

            var logLevel = typeof(T).IsAssignableTo(typeof(IQueryMessage)) ? LogLevel.Debug : LogLevel.Information;

            logger.Log(logLevel, "MassTransit consume completed {MessageType} in {ElapsedMs:0.00} ms", typeof(T).Name, stopwatch.Elapsed.TotalMilliseconds);
        }
    }

    public void Probe(ProbeContext context) => context.CreateFilterScope("consume-telemetry");
}