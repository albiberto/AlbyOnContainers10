using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using ProductInformationManager.Application.Cache;
namespace ProductInformationManager.Application;

public static class ApplicationServiceExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(ApplicationServiceExtensions).Assembly);
        return services;
    }
}