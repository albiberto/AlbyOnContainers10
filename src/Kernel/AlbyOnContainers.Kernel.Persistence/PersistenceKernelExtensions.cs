using System;
using AlbyOnContainers.Kernel.Abstraction;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AlbyOnContainers.Kernel.Persistence;

public static class PersistenceKernelExtensions
{
    public static IKernelBuilder WithEfCorePersistence<TDbContext>(this IKernelBuilder builder, Action<IServiceProvider, DbContextOptionsBuilder> configureOptions) 
        where TDbContext : DbContext
    {
        builder.Host.Services.AddSingleton<AuditableEntityInterceptor>();
        builder.Host.Services.AddSingleton<DbCommandTelemetryInterceptor>();

        builder.Host.Services.AddDbContext<TDbContext>((sp, options) =>
        {
            var auditableInterceptor = sp.GetRequiredService<AuditableEntityInterceptor>();
            var telemetryInterceptor = sp.GetRequiredService<DbCommandTelemetryInterceptor>();

            options.AddInterceptors(auditableInterceptor, telemetryInterceptor);
            configureOptions(sp, options);

            if (builder.Host.Environment.IsDevelopment())
            {
                options.EnableDetailedErrors()
                       .EnableSensitiveDataLogging();
            }
        });

        // Use Aspire hosting integration if needed, but since we manually added DbContext with connection string, 
        // we use AddNpgsqlDataSource or AddNpgsqlDbContext in typical Aspire setups. 
        // The above manual registration provides full control over Interceptors.
        
        builder.Host.Services.AddHealthChecks().AddDbContextCheck<TDbContext>();

        return builder;
    }
}
