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

        return services;
    }
}
