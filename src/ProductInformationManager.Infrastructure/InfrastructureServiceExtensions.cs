using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ProductInformationManager.Infrastructure.Interceptors;
using ProductInformationManager.Infrastructure.Options;

namespace ProductInformationManager.Infrastructure;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("productdb")
            ?? throw new InvalidOperationException("Connection string 'productdb' not found.");

        services.Configure<EfCoreObservabilityOptions>(configuration.GetSection(EfCoreObservabilityOptions.SectionName));

        services.AddSingleton<AuditableEntityInterceptor>();
        services.AddSingleton<DbCommandTelemetryInterceptor>();
        services.AddSingleton<IInterceptor>(provider => provider.GetRequiredService<AuditableEntityInterceptor>());
        services.AddSingleton<IInterceptor>(provider => provider.GetRequiredService<DbCommandTelemetryInterceptor>());

        services.AddDbContext<ProductContext>((provider, options) =>
        {
            var environment = provider.GetRequiredService<IHostEnvironment>();

            options.UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure());
            options.AddInterceptors(provider.GetServices<IInterceptor>());

            if (environment.IsDevelopment())
            {
                options.EnableDetailedErrors();
            }
        });

        // Registra l'operatore di migrazione automatica come Background Service
        services.AddHostedService<MigrationHostedService>();

        return services;
    }
}
