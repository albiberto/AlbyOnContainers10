namespace AlbyOnContainers.Plugins.DistributedLocks.HostedServices;

using System.Threading;
using System.Threading.Tasks;
using Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// Thin orchestrator: wires the messaging I/O lifecycle to the host lifetime.
/// State and reconciliation live in dedicated components.
/// </summary>
public sealed class DistributedLockHostedService(
    IRedisLockMessaging messaging,
    ILockStateTracker stateTracker,
    ILogger<DistributedLockHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // Touch the singleton tracker to guarantee its constructor (which subscribes
        // to messaging.MessageReceived) runs BEFORE messaging starts emitting.
        _ = stateTracker.Notifications;

        await messaging.StartAsync(cancellationToken);
        logger.LogDebug("Distributed lock messaging subscriber started.");
    }

    public Task StopAsync(CancellationToken cancellationToken) =>
        messaging.StopAsync(cancellationToken);
}