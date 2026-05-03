namespace AlbyOnContainers.Kernel.Persistence;

using HostedServices;
using Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Options;

public static class PersistenceKernelExtensions
{
    // ==============================================================================
    // PRIVATE STATIC HELPERS & LOGIC
    // ==============================================================================

    private static void BuildAndConfigurePersistence<TDbContext>(
        IServiceCollection services,
        Action<IServiceProvider, DbContextOptionsBuilder> configureDbContext)
        where TDbContext : DbContext
    {
        // 1. Auto-Discovery for SHARED EF Core Interceptors.
        //    SCOPED lifetime is mandatory: interceptors must access the SAME DI scope as the
        //    DbContext (e.g. IPublishEndpoint, ICurrentUserService are scoped). Singleton would
        //    force resolution via dbContext.GetInfrastructure() (EF internal SP) which does
        //    NOT forward to the application scope — yielding silent nulls and dropped events.
        //    SlowQueryInterceptor<TDbContext> is intentionally EXCLUDED from auto-discovery
        //    because it is generic and per-DbContext (different metric prefix per context).
        services.Scan(scan => scan
            .FromAssemblyOf<PersistenceOptions>()
            .AddClasses(classes => classes
                .AssignableTo<IInterceptor>()
                .Where(t => !t.IsGenericTypeDefinition))
            .AsImplementedInterfaces()
            .AsSelf()
            .WithScopedLifetime()
        );

        // 2. Per-DbContext SlowQueryInterceptor (singleton: pure metric emitter, no scoped deps).
        services.AddSingleton<SlowQueryInterceptor<TDbContext>>();

        // 3. Register DbContext
        services.AddDbContext<TDbContext>((sp, options) =>
        {
            // Delegate the DB Provider choice (e.g., UseNpgsql, UseSqlServer) to the caller
            configureDbContext(sp, options);

            // Inject Kernel Interceptors (AuditableEntityInterceptor, DomainEventDispatcher)
            // plus the per-context SlowQueryInterceptor.
            options.AddInterceptors(sp.GetServices<IInterceptor>());
            options.AddInterceptors(sp.GetRequiredService<SlowQueryInterceptor<TDbContext>>());

            var persistenceOptions = sp.GetRequiredService<IOptions<PersistenceOptions>>().Value;

            if (persistenceOptions.EnableSensitiveDataLogging)
                options.EnableSensitiveDataLogging();

            if (persistenceOptions.EnableDetailedErrors)
                options.EnableDetailedErrors();
        });

        // 4. Register Health Checks
        services.AddHealthChecks().AddDbContextCheck<TDbContext>();

        // 5. Register Auto-Migration Hosted Service.
        //    Runs in IHostedService.StartAsync, so it BLOCKS the bootstrap until migrations succeed.
        services.AddHostedService<MigrationHostedService<TDbContext>>();
    }

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
}