using AlbyOnContainers.Kernel.Domain.SeedWork;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using ProductInformationManager.Application.Mapping;

namespace ProductInformationManager.Application;

public static class ApplicationServiceExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(ApplicationServiceExtensions).Assembly);
        services.AddSingleton<IDomainEventMapper, PimDomainEventMapper>();
        return services;
    }
}