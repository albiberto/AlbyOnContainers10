using FluentValidation;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;

namespace ProductInformationManager.Application;

public static class ApplicationServiceExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Register MassTransit Mediator with auto-discovery of consumers
        services.AddMediator(cfg =>
        {
            cfg.AddConsumers(typeof(ApplicationServiceExtensions).Assembly);
        });

        // Register FluentValidation validators from this assembly
        services.AddValidatorsFromAssembly(typeof(ApplicationServiceExtensions).Assembly);

        // Register localization for validation messages
        services.AddLocalization();

        return services;
    }
}
