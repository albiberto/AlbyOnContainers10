namespace AlbyOnContainers.Kernel.Resilience.UnitTests;

using System;
using Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Options;
using NUnit.Framework;

[TestFixture]
public sealed class MessagingResilienceExtensionsTests : ResilienceTestBase
{
    [Test]
    public void WithMessagingResilience_UsingLambda_ShouldPopulateOptionsCorrectly()
    {
        // Arrange
        var expected = new ResilienceOptions
        {
            MaxRetryAttempts = 10,
            InitialDelay = TimeSpan.FromSeconds(5),
            OverallTimeout = TimeSpan.FromSeconds(60),
            UseExponentialBackoff = true
        };

        // Act
        KernelBuilder.WithMessagingResilience(opt =>
        {
            opt.MaxRetryAttempts = expected.MaxRetryAttempts;
            opt.InitialDelay = expected.InitialDelay;
            opt.OverallTimeout = expected.OverallTimeout;
            opt.UseExponentialBackoff = expected.UseExponentialBackoff;
        });

        var host = BuildHost();

        // Assert
        AssertMessaging(host, expected);
    }

    [Test]
    public void WithMessagingResilience_UsingAppSettings_ShouldBindOptionsCorrectly()
    {
        // Arrange
        HostBuilder.Configuration.AddConfiguration(Configuration);

        var expected = new ResilienceOptions
        {
            MaxRetryAttempts = 7,
            InitialDelay = TimeSpan.FromSeconds(3),
            OverallTimeout = TimeSpan.FromSeconds(50),
            UseExponentialBackoff = false
        };

        // Act
        KernelBuilder.WithMessagingResilience();

        var host = BuildHost();

        // Assert
        AssertMessaging(host, expected);
    }

    [Test]
    public void ValidateOnStart_WhenMessagingTimeoutIsZero_ShouldThrowOptionsValidationExceptionOnBuild()
    {
        // Arrange
        KernelBuilder.WithMessagingResilience(opt => opt.OverallTimeout = TimeSpan.Zero);

        // Act & Assert
        var exception = Assert.Throws<Microsoft.Extensions.Options.OptionsValidationException>(() => BuildHost());

        Assert.Multiple(() =>
        {
            Assert.That(exception, Is.Not.Null);
            Assert.That(exception.OptionsName, Is.EqualTo(nameof(ResilienceKey.Messaging)));
            Assert.That(exception.Message, Does.Contain(nameof(ResilienceOptions.OverallTimeout)));
        });
    }
    
    private static void AssertMessaging(IHost host, ResilienceOptions expectedOptions) => AssertResilienceConfiguration(host.Services, ResilienceKey.Messaging, expectedOptions);
}