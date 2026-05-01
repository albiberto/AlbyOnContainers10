using AlbyOnContainers.Kernel.Domain.SeedWork;
using Microsoft.Extensions.DependencyInjection;
using ProductInformationManager.Application.Mapping;

namespace ProductInformationManager.Application;

public static class ApplicationServiceExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<IDomainEventMapper, PimDomainEventMapper>();
        return services;
    }
}