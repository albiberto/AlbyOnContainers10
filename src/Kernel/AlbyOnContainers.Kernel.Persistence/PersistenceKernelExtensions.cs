using AlbyOnContainers.Kernel.Persistence.Interceptors;
using AlbyOnContainers.Kernel.Persistence.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AlbyOnContainers.Kernel.Persistence;

public static class PersistenceKernelExtensions
{
    extension (IKernelBuilder builder)
    {
        // ==============================================================================
        // STRICTLY TDbContext OVERLOADS ONLY
        // ==============================================================================

        public IKernelBuilder WithPersistence<TDbContext>(Action<IServiceProvider, DbContextOptionsBuilder> configureDbContext, string? section = null) where TDbContext : DbContext
        {
            builder.BindOptions(section);
            BuildAndConfigurePersistence<TDbContext>(builder.Host.Services, configureDbContext);
            return builder;
        }

        public IKernelBuilder WithPersistence<TDbContext>(Action<IServiceProvider, DbContextOptionsBuilder> configureDbContext, Action<PersistenceOptions> configureOptions) where TDbContext : DbContext
        {
            builder.ConfigureOptions(configureOptions);
            BuildAndConfigurePersistence<TDbContext>(builder.Host.Services, configureDbContext);
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
            services.AddSingleton<AuditableEntityInterceptor>();
            services.AddSingleton<DomainEventDispatcherInterceptor>();

            services.AddDbContext<TDbContext>((sp, options) =>
            {
                // Defer provider configuration (e.g. Npgsql) to the caller
                configureDbContext(sp, options);

                var persistenceOptions = sp.GetRequiredService<IOptions<PersistenceOptions>>().Value;

                options.AddInterceptors(
                    sp.GetRequiredService<AuditableEntityInterceptor>(),
                    sp.GetRequiredService<DomainEventDispatcherInterceptor>()
                );

                if (persistenceOptions.EnableSensitiveDataLogging)
                    options.EnableSensitiveDataLogging();

                if (persistenceOptions.EnableDetailedErrors)
                    options.EnableDetailedErrors();
            });

            services.AddHealthChecks().AddDbContextCheck<TDbContext>();
        }
    }
}