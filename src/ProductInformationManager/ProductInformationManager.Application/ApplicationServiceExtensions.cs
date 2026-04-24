using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using ProductInformationManager.Application.Cache;
namespace ProductInformationManager.Application;

public static class ApplicationServiceExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddApplication()
        {
            services.Scan(scan => scan
                .FromAssemblyOf<CategoryCache>()
                .AddClasses(classes => classes.InNamespaceOf<CategoryCache>())
                .AsSelf()
                .WithSingletonLifetime()
            );

            services.AddValidatorsFromAssembly(typeof(ApplicationServiceExtensions).Assembly);
            services.AddLocalization();

            return services;
        }
    }
}