namespace AlbyOnContainers.Kernel.Resilience.UnitTests;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Options;
using Polly;
using Polly.Registry;

public static class ResilienceTestingExtensions
{
    /// <summary>
    ///     Dynamically generates an IConfigurationRoot for multiple resilience profiles
    ///     and injects it into the HostBuilder. This perfectly supports chained tests.
    /// </summary>
    public static void AddInMemoryResilienceConfiguration(this IHostApplicationBuilder hostBuilder, IDictionary<string, ResilienceOptions> profiles)
    {
        var appSettings = new Dictionary<string, string?>();

        foreach (var (profileKey, options) in profiles)
        {
            appSettings.Add($"Resilience:{profileKey}:{nameof(ResilienceOptions.MaxRetryAttempts)}", options.MaxRetryAttempts.ToString());
            appSettings.Add($"Resilience:{profileKey}:{nameof(ResilienceOptions.Delay)}", options.Delay.ToString());
            appSettings.Add($"Resilience:{profileKey}:{nameof(ResilienceOptions.OverallTimeout)}", options.OverallTimeout.ToString());
            appSettings.Add($"Resilience:{profileKey}:{nameof(ResilienceOptions.UseExponentialBackoff)}", options.UseExponentialBackoff.ToString().ToLowerInvariant());
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(appSettings)
            .Build();

        hostBuilder.Configuration.AddConfiguration(configuration);
    }

    /// <summary>
    ///     Evaluates the entire DI container state for a specific resilience profile.
    /// </summary>
    public static void AssertResilienceProfile(this IServiceProvider serviceProvider, string expectedKey, ResilienceOptions expectedOptions)
    {
        var optionsMonitor = serviceProvider.GetRequiredService<IOptionsMonitor<ResilienceOptions>>();
        var actualOptions = optionsMonitor.Get(expectedKey);
        var pipelineProvider = serviceProvider.GetService<ResiliencePipelineProvider<string>>();
        var bridge = serviceProvider.GetKeyedService<ResiliencePipeline>(expectedKey);

        Assert.Multiple(() =>
        {
            Assert.That(actualOptions, Is.Not.Null, "ResilienceOptions should be resolvable.");
            Assert.That(actualOptions.MaxRetryAttempts, Is.EqualTo(expectedOptions.MaxRetryAttempts), nameof(actualOptions.MaxRetryAttempts));
            Assert.That(actualOptions.Delay, Is.EqualTo(expectedOptions.Delay), nameof(actualOptions.Delay));
            Assert.That(actualOptions.OverallTimeout, Is.EqualTo(expectedOptions.OverallTimeout), nameof(actualOptions.OverallTimeout));
            Assert.That(actualOptions.UseExponentialBackoff, Is.EqualTo(expectedOptions.UseExponentialBackoff), nameof(actualOptions.UseExponentialBackoff));

            Assert.That(pipelineProvider, Is.Not.Null, "Polly ResiliencePipelineProvider must be registered.");
            Assert.That(bridge, Is.Not.Null, $"Keyed service for {expectedKey} must be registered.");
        });
    }
}