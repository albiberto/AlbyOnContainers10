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
        services.Configure<EfCoreObservabilityOptions>(configuration.GetSection(EfCoreObservabilityOptions.SectionName));

        services.AddSingleton<DbCommandTelemetryInterceptor>();

        services.AddHostedService<MigrationHostedService>();

        return services;
    }
}
