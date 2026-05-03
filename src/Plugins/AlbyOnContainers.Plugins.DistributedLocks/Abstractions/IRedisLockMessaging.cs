namespace AlbyOnContainers.Plugins.DistributedLocks.Abstractions;

using System;
using System.Threading;
using System.Threading.Tasks;
using Model;

/// <summary>
/// Pure I/O boundary towards Redis: pub/sub channel + hash dual-write.
/// No state, no deduplication, no reactive streams.
/// </summary>
public interface IRedisLockMessaging
{
    /// <summary>Fired when a valid <see cref="LockEventPayload"/> is received from the channel.</summary>
    event Action<LockEventPayload>? MessageReceived;

    Task StartAsync(CancellationToken cancellationToken);

    Task StopAsync(CancellationToken cancellationToken);

    Task NotifyLockedAsync(string entityType, string entityId, string username);

    Task NotifyUnlockedAsync(string entityType, string entityId);
}

