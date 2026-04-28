using System;
using AlbyOnContainers.Kernel.Persistence.HostedServices;
using AlbyOnContainers.Kernel.Persistence.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AlbyOnContainers.Kernel.Persistence;

public static class PersistenceKernelExtensions
{
    // ==============================================================================
    // PUBLIC API (Fluent Builder)
    // ==============================================================================

    extension(IKernelBuilder builder)
    {
        public IKernelBuilder WithPersistence<TDbContext>(
            Action<IServiceProvider, DbContextOptionsBuilder> configureDbContext, 
            string? section = null) 
            where TDbContext : DbContext
        {
            builder.BindOptions(section);
            BuildAndConfigurePersistence<TDbContext>(builder.Host.Services, configureDbContext);
            return builder;
        }

        public IKernelBuilder WithPersistence<TDbContext>(
            Action<IServiceProvider, DbContextOptionsBuilder> configureDbContext, 
            Action<PersistenceOptions> configureOptions) 
            where TDbContext : DbContext
        {
            builder.ConfigureOptions(configureOptions);
            BuildAndConfigurePersistence<TDbContext>(builder.Host.Services, configureDbContext);
            return builder;
        }

        // ==============================================================================
        // INTERNAL OPTIONS HELPERS
        // ==============================================================================

        private void BindOptions(string? section)
        {
            builder.Host.Services
                .AddOptions<PersistenceOptions>()
                .BindConfiguration(section ?? PersistenceOptions.Section)
                .ValidateDataAnnotations()
                .ValidateOnStart();
        }

        private void ConfigureOptions(Action<PersistenceOptions> configure)
        {
            builder.Host.Services
                .AddOptions<PersistenceOptions>()
                .Configure(configure)
                .ValidateDataAnnotations()
                .ValidateOnStart();
        }
    }

    // ==============================================================================
    // PRIVATE STATIC HELPERS & LOGIC
    // ==============================================================================

    private static void BuildAndConfigurePersistence<TDbContext>(
        IServiceCollection services, 
        Action<IServiceProvider, DbContextOptionsBuilder> configureDbContext) 
        where TDbContext : DbContext
    {
        // 1. Configure Metric Prefix Default
        services.PostConfigure<PersistenceOptions>(options =>
        {
            if (string.IsNullOrWhiteSpace(options.MetricPrefix)) 
            {
                options.MetricPrefix = typeof(TDbContext).Name.ToLowerInvariant();
            }
        });

        // 2. Auto-Discovery for EF Core Interceptors
        services.Scan(scan => scan
            .FromAssemblyOf<PersistenceOptions>()
            .AddClasses(classes => classes.AssignableTo<IInterceptor>())
            .AsImplementedInterfaces()
            .WithSingletonLifetime()
        );

        // 3. Register DbContext
        services.AddDbContext<TDbContext>((sp, options) =>
        {
            // Delegate the DB Provider choice (e.g., UseNpgsql, UseSqlServer) to the caller
            configureDbContext(sp, options);

            // Inject Kernel Interceptors (AuditableEntityInterceptor, DomainEventDispatcher, SlowQuery)
            var interceptors = sp.GetServices<IInterceptor>();
            options.AddInterceptors(interceptors);

            var persistenceOptions = sp.GetRequiredService<IOptions<PersistenceOptions>>().Value;

            if (persistenceOptions.EnableSensitiveDataLogging)
                options.EnableSensitiveDataLogging();

            if (persistenceOptions.EnableDetailedErrors)
                options.EnableDetailedErrors();
        });

        // 4. Register Health Checks
        services.AddHealthChecks().AddDbContextCheck<TDbContext>();

        // 5. Register Auto-Migration Hosted Service
        // ARCHITECTURAL NOTE: The execution of migrations is controlled via PersistenceOptions.RunMigrationsOnStartup
        services.AddHostedService<MigrationHostedService<TDbContext>>();
    }
}