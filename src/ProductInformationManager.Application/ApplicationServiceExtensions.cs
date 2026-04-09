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

        return services;
    }
}
