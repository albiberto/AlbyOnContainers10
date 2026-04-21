using FluentValidation;
using MassTransit;

namespace AlbyOnContainers.Shared.Application.Infrastructure.Filters;

public class ValidationFilter<T>(IValidator<T>? validator = null) : IFilter<ConsumeContext<T>> where T : class
{
    public async Task Send(ConsumeContext<T> context, IPipe<ConsumeContext<T>> next)
    {
        if (validator is not null)
        {
            var validationResult = await validator.ValidateAsync(context.Message, context.CancellationToken);

            if (!validationResult.IsValid)
            {
                throw new ValidationException(validationResult.Errors);
            }
        }

        await next.Send(context);
    }

    public void Probe(ProbeContext context) => context.CreateFilterScope("validation");
}