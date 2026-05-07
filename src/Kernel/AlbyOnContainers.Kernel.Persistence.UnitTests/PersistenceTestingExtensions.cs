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

public static class PersistenceTestingExtensions
{
    /// <summary>
    ///     Dynamically generates an IConfigurationRoot for the primary persistence configuration
    ///     and injects it into the HostBuilder. Unlike Resilience, Persistence strictly 
    ///     uses Default Options (Singleton config per microservice), hence no Dictionary is used.
    /// </summary>
    public static void AddInMemoryPersistenceConfiguration(
        this IHostApplicationBuilder hostBuilder, 
        PersistenceOptions options, 
        string sectionName = "Persistence")
    {
        var appSettings = new Dictionary<string, string?>
        {
            { $"{sectionName}:{nameof(PersistenceOptions.MetricPrefix)}", options.MetricPrefix },
            { $"{sectionName}:{nameof(PersistenceOptions.LockTimeout)}", options.LockTimeout.ToString() },
            { $"{sectionName}:{nameof(PersistenceOptions.RunMigrationsOnStartup)}", options.RunMigrationsOnStartup.ToString() }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(appSettings)
            .Build();

        hostBuilder.Configuration.AddConfiguration(configuration);
    }

    /// <summary>
    ///     Evaluates the entire DI container state for the persistence profile.
    ///     Ensures default options are bound and opaque infrastructure is registered.
    /// </summary>
    public static void AssertPersistenceProfile<TDbContext>(this IServiceProvider serviceProvider, PersistenceOptions expectedOptions)
        where TDbContext : DbContext
    {
        // Singletons/Options can be resolved safely from the Root Provider
        var optionsMonitor = serviceProvider.GetRequiredService<IOptionsMonitor<PersistenceOptions>>();
        var actualOptions = optionsMonitor.Get(Microsoft.Extensions.Options.Options.DefaultName);

        // CREATE A SCOPE EARLY: DbContext and Interceptors are Scoped. 
        // Modern .NET strictly forbids resolving them from the Root Provider (ValidateScopes = true).
        using var scope = serviceProvider.CreateScope();
        var scopedProvider = scope.ServiceProvider;

        Assert.Multiple(() =>
        {
            // 1. Verify Options Binding
            Assert.That(actualOptions, Is.Not.Null, "PersistenceOptions should be resolvable.");
            Assert.That(actualOptions.MetricPrefix, Is.EqualTo(expectedOptions.MetricPrefix), nameof(actualOptions.MetricPrefix));
            Assert.That(actualOptions.LockTimeout, Is.EqualTo(expectedOptions.LockTimeout), nameof(actualOptions.LockTimeout));
            Assert.That(actualOptions.RunMigrationsOnStartup, Is.EqualTo(expectedOptions.RunMigrationsOnStartup), nameof(actualOptions.RunMigrationsOnStartup));

            // 2. Verify Opaque Infrastructure Registrations using the Scoped Provider
            Assert.That(scopedProvider.GetServices<TDbContext>().Any(), Is.True, "DbContext MUST be registered.");
            
            var interceptors = scopedProvider.GetServices<IInterceptor>().ToList();
            Assert.That(interceptors.Any(i => i is AuditableEntityInterceptor), Is.True, "AuditableEntityInterceptor MUST be registered.");
            Assert.That(interceptors.Any(i => i.GetType().Name == "DomainEventDispatcherInterceptor"), Is.False, "DomainEventDispatcherInterceptor belongs to Messaging, NOT Persistence.");

            // HostedServices are Singleton, so they can be resolved from the Root Provider
            Assert.That(serviceProvider.GetServices<IHostedService>().Any(h => h is MigrationHostedService<TDbContext>), Is.True, "MigrationHostedService MUST be registered for scaffolding.");

            // 3. Verify Scoped Engine Components
            var dbContext = scopedProvider.GetRequiredService<TDbContext>();
            var customizer = dbContext.GetService<IModelCustomizer>();
            
            Assert.That(customizer, Is.InstanceOf<KernelModelCustomizer>(), "The standard EF Core IModelCustomizer MUST be replaced by KernelModelCustomizer for Plugin support.");
        });
    }
}