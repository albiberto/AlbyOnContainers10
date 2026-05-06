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
    // --- PUBLIC FACADE LOGIC ---

    extension(IKernelBuilder builder)
    {
        public IKernelBuilder WithPersistence<TDbContext>(Action<IServiceProvider, DbContextOptionsBuilder> configureDbContext, string? section = null) where TDbContext : DbContext
        {
            builder.AddResilience();
            builder.BindOptions(section);
            builder.Services.BuildAndConfigurePersistence<TDbContext>(configureDbContext);

            return builder;
        }

        public IKernelBuilder WithPersistence<TDbContext>(Action<IServiceProvider, DbContextOptionsBuilder> configureDbContext, Action<PersistenceOptions> configureOptions) where TDbContext : DbContext
        {
            builder.AddResilience();
            builder.ConfigureOptions(configureOptions);
            builder.Services.BuildAndConfigurePersistence<TDbContext>(configureDbContext);

            return builder;
        }

        // --- PRIVATE INFRASTRUCTURE HELPERS ---

        private void AddResilience()
        {
            builder.WithResilience(nameof(HostedServices), options =>
            {
                options.MaxRetryAttempts = 10;
                options.Delay = TimeSpan.Zero;
                options.OverallTimeout = TimeSpan.FromSeconds(270);
                options.UseExponentialBackoff = true;
            }).WithResilience(nameof(DomainEventDispatcherInterceptor), options =>
            {
                options.MaxRetryAttempts = 3;
                options.Delay = TimeSpan.Zero;
                options.OverallTimeout = TimeSpan.FromSeconds(270);
                options.UseExponentialBackoff = true;
            });
        }
        
        private void BindOptions(string? section) =>
            builder.Host.Services
                .AddOptions<PersistenceOptions>()
                .BindConfiguration(section ?? PersistenceOptions.Section)
                .ValidateDataAnnotations()
                .ValidateOnStart();

        private void ConfigureOptions(Action<PersistenceOptions> configure) =>
            builder.Host.Services
                .AddOptions<PersistenceOptions>()
                .Configure(configure)
                .ValidateDataAnnotations()
                .ValidateOnStart();
    }

    // --- PRIVATE INFRASTRUCTURE HELPERS ---

    extension(IServiceCollection services)
    {
        private void BuildAndConfigurePersistence<TDbContext>(Action<IServiceProvider, DbContextOptionsBuilder> configureDbContext) where TDbContext : DbContext
        {
            services.Scan(scan => scan
                .FromAssemblyOf<PersistenceOptions>()
                .AddClasses(classes => classes
                    .AssignableTo<IInterceptor>()
                    .Where(t => !t.IsGenericTypeDefinition))
                .AsImplementedInterfaces()
                .AsSelf()
                .WithScopedLifetime()
            );


            services.AddDbContext<TDbContext>((sp, options) =>
            {
                configureDbContext(sp, options);

                options.AddInterceptors(sp.GetServices<IInterceptor>());

                var persistenceOptions = sp.GetRequiredService<IOptions<PersistenceOptions>>().Value;

                if (persistenceOptions.EnableSensitiveDataLogging)
                    options.EnableSensitiveDataLogging();

                if (persistenceOptions.EnableDetailedErrors)
                    options.EnableDetailedErrors();
            });

            services.AddHealthChecks().AddDbContextCheck<TDbContext>();

            services.AddHostedService<MigrationHostedService<TDbContext>>();
        }
    }
}