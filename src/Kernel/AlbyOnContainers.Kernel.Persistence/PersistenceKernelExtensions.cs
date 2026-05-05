namespace AlbyOnContainers.Kernel.Persistence;

using HostedServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Options;

public static class PersistenceKernelExtensions
{
    private static void BuildAndConfigurePersistence<TDbContext>(IServiceCollection services, Action<IServiceProvider, DbContextOptionsBuilder> configureDbContext) where TDbContext : DbContext
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
    
    extension(IKernelBuilder builder)
    {
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