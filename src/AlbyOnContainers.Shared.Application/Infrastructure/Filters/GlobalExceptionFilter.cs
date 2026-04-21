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
            logger.LogWarning("Domain rule violated in {MessageType}: {Message}", typeof(T).Name, ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Technical failure processing {MessageType}. MessageId: {MessageId}. CorrelationId: {CorrelationId}", typeof(T).Name, context.MessageId, context.CorrelationId);
            throw; 
        }
    }

    public void Probe(ProbeContext context) => context.CreateFilterScope("global-exception-logger");
}