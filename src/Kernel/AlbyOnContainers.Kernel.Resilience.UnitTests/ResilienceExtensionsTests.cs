using AlbyOnContainers.Kernel;
using AlbyOnContainers.Kernel.Resilience.Enums;
using AlbyOnContainers.Kernel.Resilience.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using Polly;
using Polly.Registry;

namespace AlbyOnContainers.Kernel.Resilience.UnitTests;

[TestFixture]
public class ResilienceExtensionsTests
{
    private sealed class TestKernelBuilder(IHostApplicationBuilder hostBuilder) : IKernelBuilder
    {
        public IHostApplicationBuilder Host { get; } = hostBuilder;
    }

    [Test]
    public void WithDatabaseResilience_UsingLambda_ShouldPopulateOptionsCorrectly()
    {
        // Arrange
        var hostBuilder = Host.CreateApplicationBuilder();
        var kernelBuilder = new TestKernelBuilder(hostBuilder);

        // Act
        kernelBuilder.WithDatabaseResilience(opt => 
        {
            opt.MaxRetryAttempts = 5;
            opt.InitialDelay = TimeSpan.FromSeconds(2);
            opt.OverallTimeout = TimeSpan.FromSeconds(45);
        });
        
        var serviceProvider = hostBuilder.Services.BuildServiceProvider();
        var optionsMonitor = serviceProvider.GetRequiredService<IOptionsMonitor<ResilienceOptions>>();
        var options = optionsMonitor.Get(ResilienceKey.Database.ToString());

        var pipelineProvider = serviceProvider.GetService<ResiliencePipelineProvider<ResilienceKey>>();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(options, Is.Not.Null);
            Assert.That(options.MaxRetryAttempts, Is.EqualTo(5));
            Assert.That(options.InitialDelay, Is.EqualTo(TimeSpan.FromSeconds(2)));
            Assert.That(options.OverallTimeout, Is.EqualTo(TimeSpan.FromSeconds(45)));
            
            // Verify pipeline provider is registered
            Assert.That(pipelineProvider, Is.Not.Null);
            
            // Verify bridge is registered
            var bridge = serviceProvider.GetKeyedService<ResiliencePipeline>(ResilienceKey.Database);
            Assert.That(bridge, Is.Not.Null);
        });
    }

    [Test]
    public void WithMessagingResilience_UsingAppSettings_ShouldBindOptionsCorrectly()
    {
        // Arrange
        var appSettings = new Dictionary<string, string?>
        {
            {"Resilience:Messaging:MaxRetryAttempts", "7"},
            {"Resilience:Messaging:InitialDelay", "00:00:03"},
            {"Resilience:Messaging:OverallTimeout", "00:00:50"},
            {"Resilience:Messaging:UseExponentialBackoff", "false"}
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(appSettings)
            .Build();

        var hostBuilder = Host.CreateApplicationBuilder();
        hostBuilder.Configuration.AddConfiguration(configuration);
        var kernelBuilder = new TestKernelBuilder(hostBuilder);

        // Act
        kernelBuilder.WithMessagingResilience();

        var serviceProvider = hostBuilder.Services.BuildServiceProvider();
        var optionsMonitor = serviceProvider.GetRequiredService<IOptionsMonitor<ResilienceOptions>>();
        var options = optionsMonitor.Get(ResilienceKey.Messaging.ToString());

        var pipelineProvider = serviceProvider.GetService<ResiliencePipelineProvider<ResilienceKey>>();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(options, Is.Not.Null);
            Assert.That(options.MaxRetryAttempts, Is.EqualTo(7));
            Assert.That(options.InitialDelay, Is.EqualTo(TimeSpan.FromSeconds(3)));
            Assert.That(options.OverallTimeout, Is.EqualTo(TimeSpan.FromSeconds(50)));
            Assert.That(options.UseExponentialBackoff, Is.False);
            
            // Verify pipeline provider is registered
            Assert.That(pipelineProvider, Is.Not.Null);
            
            // Verify bridge is registered
            var bridge = serviceProvider.GetKeyedService<ResiliencePipeline>(ResilienceKey.Messaging);
            Assert.That(bridge, Is.Not.Null);
        });
    }

    [Test]
    public void ValidateOnStart_WhenMaxRetryAttemptsIsLessThanOne_ShouldThrowOptionsValidationException()
    {
        // Arrange
        var hostBuilder = Host.CreateApplicationBuilder();
        var kernelBuilder = new TestKernelBuilder(hostBuilder);

        kernelBuilder.WithDatabaseResilience(opt => 
        {
            opt.MaxRetryAttempts = 0; // Invalid, min is 1
        });

        var serviceProvider = hostBuilder.Services.BuildServiceProvider();

        // Act & Assert
        // The exception is typically thrown when getting the options or when the host starts, 
        // due to .ValidateOnStart() in .NET 8/9/10 it happens when validating the provider
        var ex = Assert.Throws<OptionsValidationException>(() =>
        {
            // Triggering validation
            var validator = serviceProvider.GetRequiredService<IOptionsMonitor<ResilienceOptions>>().Get(ResilienceKey.Database.ToString());
        });

        Assert.That(ex?.Message, Does.Contain("Max retries must be between 1 and 10"));
    }
    
    [Test]
    public void ValidateOnStart_WhenTimeoutIsZero_ShouldThrowOptionsValidationException()
    {
        // Arrange
        var hostBuilder = Host.CreateApplicationBuilder();
        var kernelBuilder = new TestKernelBuilder(hostBuilder);

        kernelBuilder.WithMessagingResilience(opt => 
        {
            opt.OverallTimeout = TimeSpan.Zero; // Invalid, min is 5s
        });

        var serviceProvider = hostBuilder.Services.BuildServiceProvider();

        // Act & Assert
        var ex = Assert.Throws<OptionsValidationException>(() =>
        {
            // Triggering validation
            var validator = serviceProvider.GetRequiredService<IOptionsMonitor<ResilienceOptions>>().Get(ResilienceKey.Messaging.ToString());
        });

        Assert.That(ex?.Message, Does.Contain("Timeout must be configured"));
    }
}
