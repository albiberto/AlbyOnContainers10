namespace AlbyOnContainers.Kernel.Persistence.UnitTests;

using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Options;

[TestFixture]
public sealed class PersistenceExtensionsTests : PersistenceTestBase
{
    private sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options);

    private static PersistenceOptions ValidOptions => new()
    {
        MetricPrefix = "valid_prefix",
        LockTimeout = TimeSpan.FromMinutes(1),
        RunMigrationsOnStartup = true
    };

    [Test]
    public void WithPersistence_UsingConfiguration_ShouldBindOptionsAndRegisterServicesCorrectly()
    {
        // Arrange
        HostBuilder.AddInMemoryPersistenceConfiguration(ValidOptions);

        // Act
        KernelBuilder.WithPersistence<TestDbContext>((_, opts) => opts.UseInMemoryDatabase("TestDb"));
        
        var host = BuildHost();

        // Assert
        host.Services.AssertPersistenceProfile<TestDbContext>(ValidOptions);
    }

    [Test]
    public void WithPersistence_UsingLambda_ShouldBindOptionsAndRegisterServicesCorrectly()
    {
        // Arrange & Act
        KernelBuilder.WithPersistence<TestDbContext>(
            (_, opts) => opts.UseInMemoryDatabase("TestDb"), 
            opt => 
            {
                opt.MetricPrefix = ValidOptions.MetricPrefix;
                opt.LockTimeout = ValidOptions.LockTimeout;
                opt.RunMigrationsOnStartup = ValidOptions.RunMigrationsOnStartup;
            });
        
        var host = BuildHost();

        // Assert
        host.Services.AssertPersistenceProfile<TestDbContext>(ValidOptions);
    }
}