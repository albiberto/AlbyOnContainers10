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
                var errors = validationResult.Errors.Select(error => $"{error.PropertyName}: {error.ErrorMessage}").ToArray();

                logger.LogWarning("Validation failed for {MessageType}. Errors: {@ValidationErrors}", typeof(T).Name, errors);
                
                throw new ValidationException(validationResult.Errors);
            }
        }

        await next.Send(context);
    }

    public void Probe(ProbeContext context) => context.CreateFilterScope("validation");
}