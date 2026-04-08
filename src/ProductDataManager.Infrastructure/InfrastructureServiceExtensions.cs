using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using ProductDataManager.Infrastructure.Data;
using ProductDataManager.Infrastructure.Data.Interceptors;

namespace ProductDataManager.Infrastructure;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        // Register interceptors
        services.AddSingleton<IInterceptor, AuditableEntityInterceptor>();

        // Register EF Core with PostgreSQL
        services.AddDbContext<ProductContext>(options =>
            options.UseNpgsql(connectionString));

        // Register MassTransit Mediator with auto-discovery of consumers
        services.AddMediator(cfg =>
        {
            cfg.AddConsumers(typeof(ProductContext).Assembly);
        });

        return services;
    }
}
