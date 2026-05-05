namespace AlbyOnContainers.Kernel.Resilience.UnitTests;

using System;
using Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Options;
using NUnit.Framework;

[TestFixture]
public sealed class DatabaseResilienceExtensionsTests : ResilienceTestBase
{
    [Test]
    public void WithDatabaseResilience_UsingLambda_ShouldPopulateOptionsCorrectly()
    {
        // Arrange
        var expectedOptions = new ResilienceOptions
        {
            MaxRetryAttempts = 5,
            InitialDelay = TimeSpan.FromSeconds(2),
            OverallTimeout = TimeSpan.FromSeconds(45),
            UseExponentialBackoff = false
        };

        // Act
        KernelBuilder.WithDatabaseResilience(opt =>
        {
            opt.MaxRetryAttempts = expectedOptions.MaxRetryAttempts;
            opt.InitialDelay = expectedOptions.InitialDelay;
            opt.OverallTimeout = expectedOptions.OverallTimeout;
            opt.UseExponentialBackoff = expectedOptions.UseExponentialBackoff;
        });

        var host = BuildHost();

        // Assert
        AssertDatabase(host, expectedOptions);
    }

    [Test]
    public void WithDatabaseResilience_UsingAppSettings_ShouldBindOptionsCorrectly()
    {
        // Arrange
        HostBuilder.Configuration.AddConfiguration(Configuration);

        var expected = new ResilienceOptions
        {
            MaxRetryAttempts = 3,
            InitialDelay = TimeSpan.FromSeconds(1),
            OverallTimeout = TimeSpan.FromSeconds(30),
            UseExponentialBackoff = true
        };

        // Act
        KernelBuilder.WithDatabaseResilience();

        var host = BuildHost();

        // Assert
        AssertDatabase(host, expected);
    }

    [Test]
    public void ValidateOnStart_WhenDatabaseRetryIsLessThanOne_ShouldThrowOptionsValidationExceptionOnBuild()
    {
        // Arrange
        KernelBuilder.WithDatabaseResilience(opt => opt.MaxRetryAttempts = 0);

        // Act & Assert
        var exception = Assert.Throws<OptionsValidationException>(() => BuildHost());

        Assert.Multiple(() =>
        {
            Assert.That(exception, Is.Not.Null);
            Assert.That(exception!.OptionsName, Is.EqualTo(nameof(ResilienceKey.Database)));
            Assert.That(exception.Message, Does.Contain(nameof(ResilienceOptions.MaxRetryAttempts)));
        });
    }
    
    private static void AssertDatabase(Microsoft.Extensions.Hosting.IHost host, ResilienceOptions expectedOptions) 
        => AssertResilienceConfiguration(host.Services, ResilienceKey.Database, expectedOptions);
}