namespace AlbyOnContainers.Kernel.Observability.UnitTests;

using AlbyOnContainers.Kernel;
using AlbyOnContainers.Kernel.Observability;
using AlbyOnContainers.Kernel.Observability.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OpenTelemetry.Trace;

[TestFixture]
public sealed class ObservabilityExtensionsTests
{
    private static HostApplicationBuilder NewBuilder() => Host.CreateApplicationBuilder();

    [Test]
    public async Task WithObservability_Lambda_RegistersOpenTelemetryAndOptions()
    {
        // Arrange
        var builder = NewBuilder();

        // Act
        builder.AddKernel().WithObservability(opt =>
        {
            opt.EnableEntityFrameworkTracing = false;
        });

        using var host = builder.Build();
        await host.StartAsync();

        var bound = host.Services.GetRequiredService<IOptions<ObservabilityOptions>>().Value;
        var tracerProvider = host.Services.GetService<TracerProvider>();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(bound.EnableEntityFrameworkTracing, Is.False);
            Assert.That(tracerProvider, Is.Not.Null, "OpenTelemetry must be registered.");
        });

        await host.StopAsync();
    }

    [Test]
    public void WithObservability_WithSamplingProbabilityOutOfRange_FailsImmediately()
    {
        // Arrange
        var builder = NewBuilder();
        var kernel = builder.AddKernel();

        // Act & Assert
        Assert.Throws<OptionsValidationException>(() => kernel.WithObservability(opt =>
        {
            opt.TraceSamplingProbability = 2.0;
        }));
    }

    [Test]
    public async Task WithObservability_CalledTwice_DoesNotDuplicateRegistrations()
    {
        // Arrange
        var builder = NewBuilder();
        var kernel = builder.AddKernel();

        // Act
        kernel.WithObservability(opt =>
        {
            opt.EnableHttpClientTracing = false;
        });

        kernel.WithObservability(opt =>
        {
            opt.EnableHttpClientTracing = true;
        });

        using var host = builder.Build();
        await host.StartAsync();

        var bound = host.Services.GetRequiredService<IOptions<ObservabilityOptions>>().Value;

        // Assert
        Assert.That(bound.EnableHttpClientTracing, Is.False);

        await host.StopAsync();
    }
}
