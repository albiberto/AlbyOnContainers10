using AlbyOnContainers.Kernel.Domain.Exceptions;
using FluentValidation;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace AlbyOnContainers.Kernel.Messaging.Filters;

public class GlobalExceptionFilter<T>(ILogger<GlobalExceptionFilter<T>> logger) : IFilter<ConsumeContext<T>> where T : class
{
    public async Task Send(ConsumeContext<T> context, IPipe<ConsumeContext<T>> next)
    {
        try
        {
            await next.Send(context);
        }
        catch (ValidationException)
        {
            throw;
        }
        catch (DomainException ex)
        {
            logger.LogWarning("Domain rule violated in {MessageType}. Error: {Message}", typeof(T).Name, ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            using var errorScope = logger.BeginScope(new Dictionary<string, object?>
            {
                ["ErrorType"] = ex.GetType().Name
            });

            logger.LogError(ex, "Technical failure processing {MessageType}", typeof(T).Name);
            throw;
        }
    }

    public void Probe(ProbeContext context) => context.CreateFilterScope("global-exception-logger");
}