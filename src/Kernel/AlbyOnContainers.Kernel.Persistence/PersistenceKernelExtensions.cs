namespace AlbyOnContainers.Kernel.Persistence;

using Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Options;

public static class PersistenceKernelExtensions
{
    extension(IKernelBuilder builder)
    {
        // ==============================================================================
        // STRICTLY TDbContext OVERLOADS ONLY
        // ==============================================================================

        public IKernelBuilder WithPersistence<TDbContext>(Action<IServiceProvider, DbContextOptionsBuilder> configureDbContext, string? section = null) where TDbContext : DbContext
        {
            builder.BindOptions(section);
            IKernelBuilder.BuildAndConfigurePersistence<TDbContext>(builder.Host.Services, configureDbContext);
            return builder;
        }

        public IKernelBuilder WithPersistence<TDbContext>(Action<IServiceProvider, DbContextOptionsBuilder> configureDbContext, Action<PersistenceOptions> configureOptions) where TDbContext : DbContext
        {
            builder.ConfigureOptions(configureOptions);
            IKernelBuilder.BuildAndConfigurePersistence<TDbContext>(builder.Host.Services, configureDbContext);
            return builder;
        }

        // ==============================================================================
        // PRIVATE HELPERS
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

        private static void BuildAndConfigurePersistence<TDbContext>(
            IServiceCollection services,
            Action<IServiceProvider, DbContextOptionsBuilder> configureDbContext)
            where TDbContext : DbContext
        {
            // 1. Register Enterprise Interceptors as Singletons
            services.AddSingleton<AuditableEntityInterceptor>();
            services.AddSingleton<DomainEventDispatcherInterceptor>();

            // 2. Take ownership of DbContext registration.
            // This guarantees that our Enterprise configurations are ALWAYS applied correctly.
            services.AddDbContext<TDbContext>((sp, options) =>
            {
                // Execute the consuming microservice's configuration first (e.g., .UseNpgsql(connString))
                configureDbContext(sp, options);

                // Fetch the strictly-typed Kernel Persistence Options via the Fail-Fast Options Pattern
                var persistenceOptions = sp.GetRequiredService<IOptions<PersistenceOptions>>().Value;

                // Append Enterprise DDD and Auditing Interceptors
                options
                    .AddInterceptors(
                        sp.GetRequiredService<AuditableEntityInterceptor>(),
                        sp.GetRequiredService<DomainEventDispatcherInterceptor>()
                    );

                if (persistenceOptions.EnableSensitiveDataLogging) options.EnableSensitiveDataLogging();
                if (persistenceOptions.EnableDetailedErrors) options.EnableDetailedErrors();
            });

            // 3. Automatically register Health Checks for the specific DbContext
            services.AddHealthChecks().AddDbContextCheck<TDbContext>();
        }
    }
}