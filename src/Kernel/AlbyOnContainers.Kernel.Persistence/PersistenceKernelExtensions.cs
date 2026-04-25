using System;
using System.Reflection;
using System.Linq;
using AlbyOnContainers.Kernel.Abstraction;
using AlbyOnContainers.Kernel.Persistence.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace AlbyOnContainers.Kernel.Persistence;

public static class PersistenceKernelExtensions
{
    public static IKernelBuilder WithPersistence(this IKernelBuilder builder, string sectionName = PersistenceOptions.SectionName)
    {
        builder.Host.Services.AddOptions<PersistenceOptions>()
            .BindConfiguration(sectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.AddInternalPersistence(Assembly.GetCallingAssembly());
        return builder;
    }

    public static IKernelBuilder WithPersistence(this IKernelBuilder builder, Action<PersistenceOptions> configureOptions)
    {
        builder.Host.Services.AddOptions<PersistenceOptions>()
            .Configure(configureOptions)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.AddInternalPersistence(Assembly.GetCallingAssembly());
        return builder;
    }

    public static IKernelBuilder WithPersistence<TMarker>(this IKernelBuilder builder, string sectionName = PersistenceOptions.SectionName) where TMarker : DbContext
    {
        builder.Host.Services.AddOptions<PersistenceOptions>()
            .BindConfiguration(sectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Host.Services.AddSingleton<AuditableEntityInterceptor>();
        builder.Host.Services.AddSingleton<DbCommandTelemetryInterceptor>();

        builder.Host.Services.AddDbContext<TMarker>((sp, options) =>
        {
            var auditableInterceptor = sp.GetRequiredService<AuditableEntityInterceptor>();
            var telemetryInterceptor = sp.GetRequiredService<DbCommandTelemetryInterceptor>();

            options.AddInterceptors(auditableInterceptor, telemetryInterceptor);

            var persistenceOptions = sp.GetRequiredService<IOptions<PersistenceOptions>>().Value;

            if (persistenceOptions.Provider.Equals("Postgres", StringComparison.OrdinalIgnoreCase))
            {
                options.UseNpgsql(persistenceOptions.ConnectionString);
            }

            if (persistenceOptions.EnableDetailedErrors)
            {
                options.EnableDetailedErrors();
            }

            if (persistenceOptions.EnableSensitiveDataLogging)
            {
                options.EnableSensitiveDataLogging();
            }
        });

        builder.Host.Services.AddHealthChecks().AddDbContextCheck<TMarker>();

        return builder;
    }

    private static void AddInternalPersistence(this IKernelBuilder builder, Assembly scanAssembly)
    {
        builder.Host.Services.AddSingleton<AuditableEntityInterceptor>();
        builder.Host.Services.AddSingleton<DbCommandTelemetryInterceptor>();
    }
}
