namespace AlbyOnContainers.Kernel.Persistence.UnitTests;

using System;
using System.Collections.Generic;
using System.Linq;
using Customizers;
using HostedServices;
using Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using Options;
using Persistence.HostedServices;

public static class PersistenceTestingExtensions
{
    /// <summary>
    ///     Dynamically generates an IConfigurationRoot for persistence 
    ///     and injects it into the HostBuilder.
    /// </summary>
    public static void AddInMemoryPersistenceConfiguration(this IHostApplicationBuilder hostBuilder, PersistenceOptions options)
    {
        var appSettings = new Dictionary<string, string?>
        {
            { $"Persistence:{nameof(PersistenceOptions.MetricPrefix)}", options.MetricPrefix },
            { $"Persistence:{nameof(PersistenceOptions.LockTimeout)}", options.LockTimeout.ToString() },
            { $"Persistence:{nameof(PersistenceOptions.RunMigrationsOnStartup)}", options.RunMigrationsOnStartup.ToString() }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(appSettings)
            .Build();

        hostBuilder.Configuration.AddConfiguration(configuration);
    }

    /// <summary>
    ///     Evaluates the entire DI container state for the persistence profile.
    /// </summary>
    public static void AssertPersistenceProfile<TDbContext>(this IServiceProvider serviceProvider, PersistenceOptions expectedOptions)
        where TDbContext : DbContext
    {
        var optionsMonitor = serviceProvider.GetRequiredService<IOptionsMonitor<PersistenceOptions>>();
        var actualOptions = optionsMonitor.Get(Microsoft.Extensions.Options.Options.DefaultName);

        Assert.Multiple(() =>
        {
            // 1. Verify Options Binding
            Assert.That(actualOptions, Is.Not.Null, "PersistenceOptions should be resolvable.");
            Assert.That(actualOptions.MetricPrefix, Is.EqualTo(expectedOptions.MetricPrefix), nameof(actualOptions.MetricPrefix));
            Assert.That(actualOptions.LockTimeout, Is.EqualTo(expectedOptions.LockTimeout), nameof(actualOptions.LockTimeout));
            Assert.That(actualOptions.RunMigrationsOnStartup, Is.EqualTo(expectedOptions.RunMigrationsOnStartup), nameof(actualOptions.RunMigrationsOnStartup));

            // 2. Verify Opaque Infrastructure Registrations (Fail-Fast rule enforcement)
            Assert.That(serviceProvider.GetServices<TDbContext>().Any(), Is.True, "DbContext MUST be registered.");
            
            var interceptors = serviceProvider.GetServices<IInterceptor>().ToList();
            Assert.That(interceptors.Any(i => i is AuditableEntityInterceptor), Is.True, "AuditableEntityInterceptor MUST be registered.");
            Assert.That(interceptors.Any(i => i.GetType().Name == "DomainEventDispatcherInterceptor"), Is.False, "DomainEventDispatcherInterceptor belongs to Messaging, NOT Persistence.");

            Assert.That(serviceProvider.GetServices<IHostedService>().Any(h => h is MigrationHostedService<TDbContext>), Is.True, "MigrationHostedService MUST be registered for scaffolding.");

            // 3. Verify Scoped Engine Components
            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<TDbContext>();
            var customizer = dbContext.GetService<IModelCustomizer>();
            
            Assert.That(customizer, Is.InstanceOf<KernelModelCustomizer>(), "The standard EF Core IModelCustomizer MUST be replaced by KernelModelCustomizer for Plugin support.");
        });
    }
}