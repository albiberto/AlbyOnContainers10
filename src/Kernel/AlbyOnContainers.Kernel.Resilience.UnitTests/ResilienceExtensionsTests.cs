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
    ///     to ensure test data remains 100% deterministic across CI/CD runs.
    /// </summary>
    private static Dictionary<string, ResilienceOptions> GenerateTestProfiles(int profileCount = 5) =>
        Enumerable
            .Range(1, profileCount)
            .ToDictionary(
                _ => $"Profile_{Guid.NewGuid():N}",
                index => new ResilienceOptions
                {
                    MaxRetryAttempts = index * 2, // e.g., 2, 4, 6...
                    InitialDelay = TimeSpan.FromMilliseconds(index * 250), // e.g., 250ms, 500ms...
                    OverallTimeout = TimeSpan.FromSeconds(index * 10), // e.g., 10s, 20s...
                    UseExponentialBackoff = index % 2 == 0 // Alternates false/true
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
    public void ValidateOnStart_WhenRetryIsLessThanOne_ShouldThrowOptionsValidationExceptionOnBuild()
    {
        // Arrange
        var key = $"{Guid.NewGuid()}";
        KernelBuilder.WithResilience(key, opt => opt.MaxRetryAttempts = 0);

        // Act & Assert
        var exception = Assert.Throws<OptionsValidationException>(() => BuildHost());
        exception.AssertValidationException(key, nameof(ResilienceOptions.MaxRetryAttempts));
    }

    [Test]
    public void ValidateOnStart_WhenTimeoutIsZero_ShouldThrowOptionsValidationExceptionOnBuild()
    {
        // Arrange
        var key = $"{Guid.NewGuid()}";
        KernelBuilder.WithResilience(key, opt => opt.OverallTimeout = TimeSpan.Zero);

        // Act & Assert
        var exception = Assert.Throws<OptionsValidationException>(() => BuildHost());
        exception.AssertValidationException(key, nameof(ResilienceOptions.OverallTimeout));
    }
}