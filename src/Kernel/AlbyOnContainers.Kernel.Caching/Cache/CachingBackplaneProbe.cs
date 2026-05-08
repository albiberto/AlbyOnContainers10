namespace AlbyOnContainers.Kernel.Caching.Cache;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;

/// <summary>
///     Fail-fast hosted service that ensures the <see cref="IConnectionMultiplexer" /> required by the
///     FusionCache Redis backplane is registered in DI at application startup. Without this probe a
///     missing multiplexer surfaces only on the first cache access, making misconfiguration hard to diagnose.
/// </summary>
internal sealed class CachingBackplaneProbe(IServiceProvider serviceProvider, string? serviceKey) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var multiplexer = serviceKey is null
            ? serviceProvider.GetService<IConnectionMultiplexer>()
            : serviceProvider.GetKeyedService<IConnectionMultiplexer>(serviceKey);

        if (multiplexer is not null) return Task.CompletedTask;

        var keyHint = serviceKey is null ? string.Empty : $" with key '{serviceKey}'";
        throw new InvalidOperationException(
            $"WithCaching{keyHint} requires an IConnectionMultiplexer{keyHint} to be registered in DI. " +
            "Register it before AddKernel(), typically via Aspire's `builder.AddRedisClient(\"cache\")`.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
