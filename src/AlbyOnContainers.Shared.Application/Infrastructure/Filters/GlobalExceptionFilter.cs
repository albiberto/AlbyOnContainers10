using AlbyOnContainers.Shared.Domain;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace AlbyOnContainers.Shared.Application.Infrastructure.Filters;

public class GlobalExceptionFilter<T>(ILogger<GlobalExceptionFilter<T>> logger) : IFilter<ConsumeContext<T>> where T : class
{
    public async Task Send(ConsumeContext<T> context, IPipe<ConsumeContext<T>> next)
    {
        try
        {
            await next.Send(context);
        }
        catch (FluentValidation.ValidationException)
        {
            throw; 
        }
        catch (DomainException ex)
        {
            logger.LogWarning(
                "PIM MassTransit domain rule violated in {MessageType}. MessageId: {MessageId}. CorrelationId: {CorrelationId}. Error: {Message}",
                typeof(T).Name,
                context.MessageId,
                context.CorrelationId,
                ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "PIM MassTransit technical failure processing {MessageType}. MessageId: {MessageId}. CorrelationId: {CorrelationId}",
                typeof(T).Name,
                context.MessageId,
                context.CorrelationId);
            throw; 
        }
    }

    public void Probe(ProbeContext context) => context.CreateFilterScope("global-exception-logger");
}
