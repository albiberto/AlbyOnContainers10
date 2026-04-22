using FluentValidation;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace AlbyOnContainers.Shared.Application.Infrastructure.Filters;

public class ValidationFilter<T>(ILogger<ValidationFilter<T>> logger, IValidator<T>? validator = null) : IFilter<ConsumeContext<T>> where T : class
{
    public async Task Send(ConsumeContext<T> context, IPipe<ConsumeContext<T>> next)
    {
        if (validator is not null)
        {
            var validationResult = await validator.ValidateAsync(context.Message, context.CancellationToken);

            if (!validationResult.IsValid)
            {
                logger.LogWarning(
                    "PIM MassTransit validation failed for {MessageType}. MessageId: {MessageId}. Errors: {Errors}",
                    typeof(T).Name,
                    context.MessageId,
                    validationResult.Errors.Select(error => $"{error.PropertyName}: {error.ErrorMessage}").ToArray());
                throw new ValidationException(validationResult.Errors);
            }
        }

        await next.Send(context);
    }

    public void Probe(ProbeContext context) => context.CreateFilterScope("validation");
}
