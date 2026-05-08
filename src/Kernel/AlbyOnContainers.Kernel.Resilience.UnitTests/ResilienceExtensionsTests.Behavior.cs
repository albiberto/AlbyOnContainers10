namespace AlbyOnContainers.Kernel.Resilience.UnitTests;

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Polly;
using Polly.CircuitBreaker;

[TestFixture]
public sealed class ResilienceExtensionsBehaviorTests : ResilienceTestBase
{
    [Test]
    public async Task Pipeline_WhenCallbackThrowsOperationCanceled_RunsExactlyOnce()
    {
        // Arrange
        const string key = "no-retry-on-cancel-counted";
        KernelBuilder.WithResilience(key, opt =>
        {
            opt.MaxRetryAttempts = 5;
            opt.Delay = TimeSpan.FromSeconds(1);
            opt.OverallTimeout = TimeSpan.FromSeconds(30);
            opt.UseExponentialBackoff = false;
        });

        var host = BuildHost();
        var pipeline = host.Services.GetRequiredKeyedService<ResiliencePipeline>(key);

        var attempts = 0;

        // Act
        try
        {
            await pipeline.ExecuteAsync(_ =>
            {
                Interlocked.Increment(ref attempts);
                throw new OperationCanceledException();
            });
            Assert.Fail("Expected OperationCanceledException to propagate.");
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert
        Assert.That(attempts, Is.EqualTo(1), "OperationCanceledException must NOT be retried.");
    }

    [Test]
    public async Task Pipeline_WithoutCircuitBreaker_DoesNotThrowBrokenCircuitException()
    {
        // Arrange
        const string key = "no-circuit-breaker";
        KernelBuilder.WithResilience(key, opt =>
        {
            opt.MaxRetryAttempts = 1;
            opt.Delay = TimeSpan.FromSeconds(1);
            opt.OverallTimeout = TimeSpan.FromSeconds(30);
            opt.UseExponentialBackoff = false;
        });

        var host = BuildHost();
        var pipeline = host.Services.GetRequiredKeyedService<ResiliencePipeline>(key);

        // Act
        Exception? observed = null;
        try
        {
            await pipeline.ExecuteAsync(_ => throw new InvalidOperationException("boom"));
        }
        catch (Exception ex)
        {
            observed = ex;
        }

        // Assert
        Assert.That(observed, Is.Not.Null);
        Assert.That(observed, Is.Not.InstanceOf<BrokenCircuitException>(), "Without opt-in CircuitBreaker no breaker should be active.");
        Assert.That(observed, Is.InstanceOf<InvalidOperationException>());
    }

    [Test]
    public async Task Pipeline_WithCircuitBreaker_OpensAfterFailureRatioReached()
    {
        // Arrange — aggressive breaker so the test is fast and deterministic
        const string key = "with-circuit-breaker";
        KernelBuilder.WithResilience(key, opt =>
        {
            opt.MaxRetryAttempts = 1;
            opt.Delay = TimeSpan.FromSeconds(1);
            opt.OverallTimeout = TimeSpan.FromSeconds(30);
            opt.UseExponentialBackoff = false;
            opt.CircuitBreaker = new()
            {
                FailureRatio = 0.5,
                MinimumThroughput = 2,
                BreakDuration = TimeSpan.FromSeconds(5),
                SamplingDuration = TimeSpan.FromSeconds(5)
            };
        });

        var host = BuildHost();
        var pipeline = host.Services.GetRequiredKeyedService<ResiliencePipeline>(key);

        // Act — fire enough failing calls to satisfy MinimumThroughput at 100% failure ratio
        for (var i = 0; i < 4; i++)
        {
            try
            {
                await pipeline.ExecuteAsync(_ => throw new InvalidOperationException($"failure-{i}"));
            }
            catch
            {
                // Swallow — we only care about the breaker state at the end.
            }
        }

        // Assert — the next call must short-circuit with BrokenCircuitException
        Assert.ThrowsAsync<BrokenCircuitException>(async () =>
            await pipeline.ExecuteAsync(_ => ValueTask.CompletedTask));
    }
}
