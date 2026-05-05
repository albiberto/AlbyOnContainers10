namespace AlbyOnContainers.Kernel.Resilience.UnitTests;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Options;

[TestFixture]
public sealed class ResilienceExtensionsTests : ResilienceTestBase
{
    /// <summary>
    ///     Generates a dynamic set of deterministic test profiles to stress-test
    ///     the DI container's Keyed Services isolation and options binding.
    ///     Uses Guids to prove keys are completely arbitrary, and algorithmic math
    ///     to ensure test data remains strictly within DataAnnotation boundaries.
    /// </summary>
    private static Dictionary<string, ResilienceOptions> GenerateTestProfiles(int profileCount = 5) =>
        Enumerable
            .Range(1, profileCount)
            .ToDictionary(
                _ => $"Profile_{Guid.NewGuid():N}",

                // Algorithmic data generation ensuring DataAnnotations validity
                index => new ResilienceOptions
                {
                    MaxRetryAttempts = index,
                    InitialDelay = TimeSpan.FromSeconds(index),
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
        foreach (var profileKey in profiles.Keys) KernelBuilder.WithResilience(profileKey);

        var host = BuildHost();

        // Assert: Verify each profile is perfectly isolated within its DI Key
        foreach (var (key, expectedOptions) in profiles) host.Services.AssertResilienceProfile(key, expectedOptions);
    }

    [Test]
    public void WithResilience_ChainedRegistrations_UsingLambda_ShouldBindAllProfilesCorrectly()
    {
        // Arrange
        var profiles = GenerateTestProfiles();

        // Act
        foreach (var (key, expected) in profiles)
            KernelBuilder.WithResilience(key, opt =>
            {
                opt.MaxRetryAttempts = expected.MaxRetryAttempts;
                opt.InitialDelay = expected.InitialDelay;
                opt.OverallTimeout = expected.OverallTimeout;
                opt.UseExponentialBackoff = expected.UseExponentialBackoff;
            });

        var host = BuildHost();

        // Assert: Verify lambda configurations did not overwrite each other
        foreach (var (key, expectedOptions) in profiles) host.Services.AssertResilienceProfile(key, expectedOptions);
    }

    [Test]
    public void ValidateOnStart_WhenRetryIsLessThanOne_ShouldThrowOptionsValidationException()
    {
        // Arrange
        var key = $"FaultyProfile_{Guid.NewGuid():N}";

        KernelBuilder.WithResilience(key, opt => opt.MaxRetryAttempts = 0); // Invalid boundary

        var host = BuildHost();

        // Act & Assert
        var exception = Assert.Throws<OptionsValidationException>(() => host.Services.GetRequiredService<IOptionsMonitor<ResilienceOptions>>().Get(key));
        exception.AssertValidationException(key, nameof(ResilienceOptions.MaxRetryAttempts));
    }

    [Test]
    public void ValidateOnStart_WhenTimeoutIsZero_ShouldThrowOptionsValidationException()
    {
        // Arrange
        var key = $"FaultyProfile_{Guid.NewGuid():N}";

        KernelBuilder.WithResilience(key, opt => opt.OverallTimeout = TimeSpan.Zero); // Invalid boundary

        var host = BuildHost();

        // Act & Assert
        var exception = Assert.Throws<OptionsValidationException>(() => host.Services.GetRequiredService<IOptionsMonitor<ResilienceOptions>>().Get(key));
        exception.AssertValidationException(key, nameof(ResilienceOptions.OverallTimeout));
    }
}