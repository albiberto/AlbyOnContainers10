namespace AlbyOnContainers.Kernel.Resilience.UnitTests;

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Options;

[TestFixture]
public sealed class ResilienceExtensionsValidTests : ResilienceTestBase
{
    private static Dictionary<string, ResilienceOptions> GenerateTestProfiles(int profileCount = 5) =>
        Enumerable
            .Range(1, profileCount)
            .ToDictionary(
                _ => $"{Guid.NewGuid():N}",
                index => new ResilienceOptions
                {
                    MaxRetryAttempts = index,
                    Delay = TimeSpan.FromSeconds(index),
                    OverallTimeout = TimeSpan.FromSeconds(10 + index),
                    UseExponentialBackoff = index % 2 == 0
                });

    [Test]
    public void WithResilience_ChainedRegistrations_UsingConfiguration_ShouldBindAllProfilesCorrectly()
    {
        // Arrange
        var profiles = GenerateTestProfiles();
        HostBuilder.AddInMemoryResilienceConfiguration(profiles);

        // Act
        foreach (var key in profiles.Keys) KernelBuilder.WithResilience(key);
        
        var host = BuildHost();

        // Assert
        foreach (var (key, expected) in profiles) host.Services.AssertResilienceProfile(key, expected);
    }

    [Test]
    public void WithResilience_ChainedRegistrations_UsingLambda_ShouldBindAllProfilesCorrectly()
    {
        // Arrange
        var profiles = GenerateTestProfiles();

        // Act
        foreach (var (key, option) in profiles)
        {
            KernelBuilder.WithResilience(key, opt =>
            {
                opt.MaxRetryAttempts = option.MaxRetryAttempts;
                opt.Delay = option.Delay;
                opt.OverallTimeout = option.OverallTimeout;
                opt.UseExponentialBackoff = option.UseExponentialBackoff;
            });
        }

        var host = BuildHost();

        // Assert
        foreach (var (key, expected) in profiles) host.Services.AssertResilienceProfile(key, expected);
    }

    [Test]
    public void WithResilience_OptInCircuitBreaker_UsingLambda_ShouldBindNestedOptions()
    {
        // Arrange
        const string key = "with-circuit-breaker";
        var expected = new Options.ResilienceOptions
        {
            MaxRetryAttempts = 3,
            Delay = TimeSpan.FromSeconds(1),
            OverallTimeout = TimeSpan.FromSeconds(10),
            UseExponentialBackoff = true,
            CircuitBreaker = new()
            {
                FailureRatio = 0.5,
                MinimumThroughput = 5,
                BreakDuration = TimeSpan.FromSeconds(10),
                SamplingDuration = TimeSpan.FromSeconds(30)
            }
        };

        // Act
        KernelBuilder.WithResilience(key, opt =>
        {
            opt.MaxRetryAttempts = expected.MaxRetryAttempts;
            opt.Delay = expected.Delay;
            opt.OverallTimeout = expected.OverallTimeout;
            opt.UseExponentialBackoff = expected.UseExponentialBackoff;
            opt.CircuitBreaker = expected.CircuitBreaker;
        });

        var host = BuildHost();

        // Assert
        host.Services.AssertResilienceProfile(key, expected);
    }
}