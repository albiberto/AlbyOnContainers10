namespace AlbyOnContainers.Kernel.Resilience.UnitTests;

using Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Options;
using Polly;
using Polly.Registry;
using NUnit.Framework;

/// <summary>
///     Base class for resilience tests.
///     Manages the strict lifecycle of the HostBuilder and provides shared assertion logic.
/// </summary>
public abstract class ResilienceTestBase
{
    private IHost? _host;
    
    protected HostApplicationBuilder HostBuilder = null!;
    protected IKernelBuilder KernelBuilder = null!;

    protected static IConfigurationRoot Configuration =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Messaging Settings
                { "Resilience:Messaging:MaxRetryAttempts", "7" },
                { "Resilience:Messaging:InitialDelay", "00:00:03" },
                { "Resilience:Messaging:OverallTimeout", "00:00:50" },
                { "Resilience:Messaging:UseExponentialBackoff", "false" },
                
                // Database Settings
                { "Resilience:Database:MaxRetryAttempts", "3" },
                { "Resilience:Database:InitialDelay", "00:00:01" },
                { "Resilience:Database:OverallTimeout", "00:00:30" },
                { "Resilience:Database:UseExponentialBackoff", "true" }
            })
            .Build();

    [SetUp]
    public void SetUpBase()
    {
        HostBuilder = Host.CreateApplicationBuilder();
        KernelBuilder = HostBuilder.AddKernel();
    }

    [TearDown]
    public void TearDownBase() => _host?.Dispose();

    protected IHost BuildHost()
    {
        _host = HostBuilder.Build();
        return _host;
    }

    /// <summary>
    ///     Evaluates the entire DI container state to ensure the Fluent Builder correctly
    ///     mapped the options and registered the underlying Polly infrastructure.
    /// </summary>
    protected static void AssertResilienceConfiguration(IServiceProvider serviceProvider, ResilienceKey expectedKey, ResilienceOptions expectedOptions)
    {
        var optionsMonitor = serviceProvider.GetRequiredService<IOptionsMonitor<ResilienceOptions>>();
        var actualOptions = optionsMonitor.Get(expectedKey.ToString());
        var pipelineProvider = serviceProvider.GetService<ResiliencePipelineProvider<ResilienceKey>>();
        var bridge = serviceProvider.GetKeyedService<ResiliencePipeline>(expectedKey);

        Assert.Multiple(() =>
        {
            // Verify Options
            Assert.That(actualOptions, Is.Not.Null, "ResilienceOptions should be resolvable.");
            Assert.That(actualOptions.MaxRetryAttempts, Is.EqualTo(expectedOptions.MaxRetryAttempts), nameof(actualOptions.MaxRetryAttempts));
            Assert.That(actualOptions.InitialDelay, Is.EqualTo(expectedOptions.InitialDelay), nameof(actualOptions.InitialDelay));
            Assert.That(actualOptions.OverallTimeout, Is.EqualTo(expectedOptions.OverallTimeout), nameof(actualOptions.OverallTimeout));
            Assert.That(actualOptions.UseExponentialBackoff, Is.EqualTo(expectedOptions.UseExponentialBackoff), nameof(actualOptions.UseExponentialBackoff));

            // Verify Opaque Infrastructure Registration
            Assert.That(pipelineProvider, Is.Not.Null, "Polly ResiliencePipelineProvider must be registered.");
            Assert.That(bridge, Is.Not.Null, $"Keyed service for {expectedKey} must be registered.");
        });
    }
}