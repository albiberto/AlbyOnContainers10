// ReSharper disable once CheckNamespace

namespace Microsoft.Extensions.DependencyInjection;

using AlbyOnContainers.Kernel;
using AlbyOnContainers.Kernel.Persistence.Customizers;
using AlbyOnContainers.Kernel.Persistence.HostedServices;
using AlbyOnContainers.Kernel.Persistence.Interceptors;
using AlbyOnContainers.Kernel.Persistence.Options;
using EntityFrameworkCore;
using EntityFrameworkCore.Diagnostics;
using EntityFrameworkCore.Infrastructure;
using Options;

public static class PersistenceKernelExtensions
{
    extension(IKernelBuilder builder)
    {
        public IKernelBuilder WithPersistence<TDbContext>(Action<IServiceProvider, DbContextOptionsBuilder> configureDbContext, string? section = null) where TDbContext : DbContext
        {
            builder.AddResilience();
            builder.BindOptions(section);
            builder.BuildAndConfigurePersistence<TDbContext>(configureDbContext);

            return builder;
        }

        public IKernelBuilder WithPersistence<TDbContext>(Action<IServiceProvider, DbContextOptionsBuilder> configureDbContext, Action<PersistenceOptions> configureOptions) where TDbContext : DbContext
        {
            builder.AddResilience();
            builder.ConfigureOptions(configureOptions);
            builder.BuildAndConfigurePersistence<TDbContext>(configureDbContext);

            return builder;
        }

        private void AddResilience() =>
            builder
                .WithResilience(
                    nameof(MigrationHostedService<>), options =>
                    {
                        options.MaxRetryAttempts = 10;
                        options.Delay = TimeSpan.Zero;
                        options.OverallTimeout = TimeSpan.FromSeconds(270);
                        options.UseExponentialBackoff = true;
                    })
                .WithResilience(
                    nameof(DomainEventDispatcherInterceptor), options =>
                    {
                        options.MaxRetryAttempts = 3;
                        options.Delay = TimeSpan.Zero;
                        options.OverallTimeout = TimeSpan.FromSeconds(270);
                        options.UseExponentialBackoff = true;
                    });

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

        private void BuildAndConfigurePersistence<TDbContext>(Action<IServiceProvider, DbContextOptionsBuilder> configureDbContext) where TDbContext : DbContext
        {
            builder.Services.AddScoped<IInterceptor, AuditableInterceptor>();
            builder.Services.AddScoped<IInterceptor, DomainEventDispatcherInterceptor>();

            builder.Services.AddDbContext<TDbContext>((sp, options) =>
            {
                configureDbContext(sp, options);

                options.ReplaceService<IModelCustomizer, KernelModelCustomizer>();

                options.AddInterceptors(sp.GetServices<IInterceptor>());

                var persistenceOptions = sp.GetRequiredService<IOptions<PersistenceOptions>>().Value;

                if (persistenceOptions.EnableSensitiveDataLogging)
                    options.EnableSensitiveDataLogging();

                if (persistenceOptions.EnableDetailedErrors)
                    options.EnableDetailedErrors();
            });

            builder.Services.AddHealthChecks().AddDbContextCheck<TDbContext>();

            builder.Services.AddHostedService<MigrationHostedService<TDbContext>>();
        }
    }
}