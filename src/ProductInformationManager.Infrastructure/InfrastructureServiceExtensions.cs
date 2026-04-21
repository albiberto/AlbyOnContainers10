using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using ProductInformationManager.Infrastructure.Interceptors;

namespace ProductInformationManager.Infrastructure;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddSingleton<IInterceptor, AuditableEntityInterceptor>();
        services.AddDbContext<ProductContext>(options => options.UseNpgsql(connectionString));

        // Registra l'operatore di migrazione automatica come Background Service
        services.AddHostedService<MigrationHostedService>();

        return services;
    }
}