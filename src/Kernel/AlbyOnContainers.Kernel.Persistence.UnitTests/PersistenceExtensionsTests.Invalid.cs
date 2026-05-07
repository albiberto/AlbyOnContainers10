namespace AlbyOnContainers.Kernel.Persistence.UnitTests;

using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using Options;

[TestFixture]
public sealed class PersistenceExtensionsInvalidTests : PersistenceTestBase
{
    private sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options);

    private static PersistenceOptions ValidOptions => new()
    {
        MetricPrefix = "valid_prefix",
        LockTimeout = TimeSpan.FromMinutes(1),
        RunMigrationsOnStartup = true
    };
    
    private static IEnumerable<TestCaseData> InvalidOptionsSource() =>
    [
        // MetricPrefix Boundaries (Required & Validations)
        CreateTestCase("MetricPrefix_Empty", ValidOptions with { MetricPrefix = string.Empty }),
        CreateTestCase("MetricPrefix_StartsWithNumber", ValidOptions with { MetricPrefix = "1invalid" }),
        CreateTestCase("MetricPrefix_SpecialChars", ValidOptions with { MetricPrefix = "invalid-prefix!" }),
        
        // LockTimeout Boundaries
        CreateTestCase("LockTimeout_BelowMinimum", ValidOptions with { LockTimeout = TimeSpan.FromSeconds(4) }),
        CreateTestCase("LockTimeout_AboveMaximum", ValidOptions with { LockTimeout = TimeSpan.FromMinutes(6) })
    ];
    
    private static TestCaseData CreateTestCase(string key, PersistenceOptions options) => new TestCaseData(key, options).SetName($"Invalid_{key}");

    [TestCaseSource(nameof(InvalidOptionsSource))]
    public void WithPersistence_InvalidConfiguration_ShouldThrowOptionsValidationException(string key, PersistenceOptions options)
    {
        // Arrange
        HostBuilder.AddInMemoryPersistenceConfiguration(options);
        
        // Act
        KernelBuilder.WithPersistence<TestDbContext>((_, opts) => opts.UseInMemoryDatabase("TestDb"));
        
        var host = BuildHost();

        // Assert - Fail Fast at boot time
        Assert.Throws<OptionsValidationException>(() => host.Services.GetRequiredService<IOptionsMonitor<PersistenceOptions>>().Get(Microsoft.Extensions.Options.Options.DefaultName));
    }
    
    [TestCaseSource(nameof(InvalidOptionsSource))]
    public void WithPersistence_InvalidLambdaOptions_ShouldThrowOptionsValidationException(string label, PersistenceOptions options)
    {
        // Arrange & Act
        KernelBuilder.WithPersistence<TestDbContext>(
            (_, opts) => opts.UseInMemoryDatabase("TestDb"),
            opt =>
            {
                opt.MetricPrefix = options.MetricPrefix;
                opt.LockTimeout = options.LockTimeout;
                opt.RunMigrationsOnStartup = options.RunMigrationsOnStartup;
            });
        
        var host = BuildHost();

        // Assert - Fail Fast at boot time
        Assert.Throws<OptionsValidationException>(() =>
            host.Services.GetRequiredService<IOptionsMonitor<PersistenceOptions>>().Get(Microsoft.Extensions.Options.Options.DefaultName));
    }
}