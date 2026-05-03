using System.Collections.Concurrent;
using Medallion.Threading;
using Microsoft.Extensions.Options;
using System.Reactive.Linq;
using AlbyOnContainers.Plugins.DistributedLocks.Abstractions;
using AlbyOnContainers.Plugins.DistributedLocks.Model;
using AlbyOnContainers.Plugins.DistributedLocks.Options;
using Microsoft.Extensions.Logging;

namespace AlbyOnContainers.Plugins.DistributedLocks;

/// <summary>
/// Per-entity distributed lock notifier.
/// Local mutual exclusion is intentionally NOT enforced via SemaphoreSlim:
/// the underlying Redis distributed lock (Medallion) is itself a mutex even
/// across threads of the same process, so a local semaphore would only add
/// surface for race conditions (refcount disposal vs. waiters).
/// </summary>
public sealed class LockNotifier<TEntity>(
    IDistributedLockProvider lockProvider,
    IRedisLockMessaging messaging,
    ILockStateTracker stateTracker,
    LockTracker<TEntity> tracker,
    IOptions<DistributedLockOptions> options,
    ILogger<LockNotifier<TEntity>> logger)
    : IAsyncDisposable
{
    private readonly DistributedLockOptions _config = options.Value;
    private readonly string _entityType = typeof(TEntity).Name;
    private readonly ConcurrentDictionary<string, IDistributedSynchronizationHandle> _locks = new();

    public IObservable<Emit> Changes => stateTracker.Notifications.Where(n => n.EntityType == _entityType);

    public bool IsLockedBy(string entityId, out string? username) => tracker.IsLocked(entityId, out username);

    public async Task<bool> TryAcquireLockAsync(string entityId, string username)
    {
        // Fast path: lock already held by this notifier instance.
        if (_locks.ContainsKey(entityId)) return true;

        var lockName = $"{_config.KeyPrefix}{_entityType}:{entityId}";
        var handle = await lockProvider.TryAcquireLockAsync(lockName, _config.AcquireTimeout);

        if (handle is null) return false;

        // Redis lock is a global mutex => only one thread (across the cluster) can be here
        // for a given entityId. TryAdd should always succeed; the defensive branch covers
        // the pathological case where another concurrent acquisition won the local race
        // (theoretically impossible, but kept for safety).
        if (_locks.TryAdd(entityId, handle))
        {
            await messaging.NotifyLockedAsync(_entityType, entityId, username);
            return true;
        }

        await handle.DisposeAsync();
        return true;
    }

    public async Task ReleaseLockAsync(string entityId)
    {
        if (!_locks.TryRemove(entityId, out var handle)) return;

        await handle.DisposeAsync();
        await messaging.NotifyUnlockedAsync(_entityType, entityId);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var kvp in _locks)
        {
            try
            {
                await kvp.Value.DisposeAsync();
                await messaging.NotifyUnlockedAsync(_entityType, kvp.Key);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to release lock for {EntityType}:{EntityId} during disposal.", _entityType, kvp.Key);
            }
        }

        _locks.Clear();
    }
}