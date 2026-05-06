namespace AlbyOnContainers.Kernel.Persistence.Interceptors;

using Microsoft.Extensions.Logging;

public sealed partial class DomainEventDispatcherInterceptor
{
    [LoggerMessage(
        EventId = 101,
        Level = LogLevel.Debug,
        Message = "Processing Domain Event: {EventName}")]
    private partial void LogProcessingDomainEvent(string eventName);

    [LoggerMessage(
        EventId = 102,
        Level = LogLevel.Debug,
        Message = "Domain Event {EventName} produced no Integration Messages.")]
    private partial void LogNoIntegrationMessagesProduced(string eventName);
}