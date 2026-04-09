using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using ProductDataManager.Infrastructure.Interceptors;
using ProductInformationManager.Infrastructure;

namespace ProductDataManager.Infrastructure;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddSingleton<IInterceptor, AuditableEntityInterceptor>();
        services.AddDbContext<ProductContext>(options => options.UseNpgsql(connectionString));

        return services;
    }
}
