using AlbyOnContainers.Kernel.Resilience.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AlbyOnContainers.Kernel.Resilience.UnitTests;

using static Assert;

[TestFixture]
public sealed class ResilienceExtensionsInvalidTests : ResilienceTestBase
{
    private static ResilienceOptions ValidOptions => new()
    {
        MaxRetryAttempts = 5,
        Delay = TimeSpan.FromSeconds(5),
        OverallTimeout = TimeSpan.FromSeconds(10),
        UseExponentialBackoff = true
    };
    
    private static IEnumerable<TestCaseData> InvalidScenariosSource() =>
    [
        // MaxRetryAttempts Boundaries [Range(1, 10)]
        CreateTestCase("MaxRetryAttempts_BelowMinimum", ValidOptions with { MaxRetryAttempts = 0 }),
        CreateTestCase("MaxRetryAttempts_AboveMaximum", ValidOptions with { MaxRetryAttempts = 11 }),

        // Delay Boundaries [Range("00:00:01", "00:00:10")]
        CreateTestCase("Delay_BelowMinimum", ValidOptions with { Delay = TimeSpan.Zero }),
        CreateTestCase("Delay_AboveMaximum", ValidOptions with { Delay = TimeSpan.FromSeconds(11) }),

        // OverallTimeout Boundaries [Range("00:00:05", "00:01:00")]
        CreateTestCase("OverallTimeout_BelowMinimum", ValidOptions with { OverallTimeout = TimeSpan.FromSeconds(4) }),
        CreateTestCase("OverallTimeout_AboveMaximum", ValidOptions with { OverallTimeout = TimeSpan.FromSeconds(61) })
    ];
    
    private static TestCaseData CreateTestCase(string key, ResilienceOptions options) => new TestCaseData(key, options).SetName($"Invalid_{key}");

    [TestCaseSource(nameof(InvalidScenariosSource))]
    public void WithResilience_InvalidConfiguration_ShouldThrowOptionsValidationException(string key, ResilienceOptions options)
    {
        // Arrange
        HostBuilder.AddInMemoryResilienceConfiguration(new Dictionary<string, ResilienceOptions>
        {
            [key] = options
        });
        
        // Act
        KernelBuilder.WithResilience(key);
        
        var host = BuildHost();

        // Assert
        Throws<OptionsValidationException>(() => host.Services.GetRequiredService<IOptionsMonitor<ResilienceOptions>>().Get(key));
    }
    
    [TestCaseSource(nameof(InvalidScenariosSource))]
    public void WithResilience_InvalidOptions_ShouldThrowOptionsValidationException(string label, ResilienceOptions options)
    {
        // Arrange & Act
        
        KernelBuilder.WithResilience(label, opt =>
        {
            opt.MaxRetryAttempts = options.MaxRetryAttempts;
            opt.Delay = options.Delay;
            opt.OverallTimeout = options.OverallTimeout;
            opt.UseExponentialBackoff = options.UseExponentialBackoff;
        });
        
        var host = BuildHost();

        // Assert
        Throws<OptionsValidationException>(() => host.Services.GetRequiredService<IOptionsMonitor<ResilienceOptions>>().Get(label));
            
    }
}