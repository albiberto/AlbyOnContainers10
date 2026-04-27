using FluentValidation;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace AlbyOnContainers.Kernel.Messaging.Filters;

public sealed class ValidationFilter<T>(
    ILogger<ValidationFilter<T>> logger,
    IValidator<T>? validator = null) : IFilter<ConsumeContext<T>> where T : class
{
    public async Task Send(ConsumeContext<T> context, IPipe<ConsumeContext<T>> next)
    {
        if (validator is null)
        {
            logger.LogDebug("[Messaging] No validator registered for message type {MessageType}. Skipping validation.", typeof(T).Name);
            await next.Send(context);
            return;
        }

        var result = await validator.ValidateAsync(context.Message, context.CancellationToken);

        if (!result.IsValid) throw new ValidationException(result.Errors);

        await next.Send(context);
    }

    public void Probe(ProbeContext context) => context.CreateFilterScope("validation");
}