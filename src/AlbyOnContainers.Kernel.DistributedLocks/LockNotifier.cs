using System.Collections.Concurrent;
using Medallion.Threading;
using Microsoft.Extensions.Options;
using System.Reactive.Linq;
using AlbyOnContainers.Kernel.DistributedLocks.Model;
using AlbyOnContainers.Kernel.DistributedLocks.Options;

namespace AlbyOnContainers.Kernel.DistributedLocks;

public sealed class LockNotifier<TEntity>(IDistributedLockProvider lockProvider, DistributedLockHostedService hostedService, LockTracker<TEntity> tracker, IOptions<DistributedLockOptions> options) : IAsyncDisposable
{
    private readonly DistributedLockOptions _config = options.Value;
    
    private readonly ConcurrentDictionary<string, IDistributedSynchronizationHandle> _locks = [];
    private readonly string _entityType = typeof(TEntity).Name;

    // 1. Reactive Query (Push Events)
    public IObservable<Emit> Changes => hostedService.Notifications.Where(n => n.EntityType == _entityType);

    // 2. Synchronous Query (Actual in memory state through the Tracker)
    public bool IsLockedBy(string entityId, out string? username) => tracker.IsLocked(entityId, out username);

    // 3. Commands (Redis Mutations)
    public async Task<bool> TryAcquireLockAsync(string entityId, string userId)
    {
        if (_locks.ContainsKey(entityId)) return true;

        var lockName = $"{_config.KeyPrefix}{_entityType}:{entityId}";
        var handle = await lockProvider.TryAcquireLockAsync(lockName, _config.AcquireTimeout);

        if (handle is null) return false;
        
        _locks.TryAdd(entityId, handle);
        await hostedService.NotifyLockedAsync(_entityType, entityId, userId);

        return true;
    }

    public async Task ReleaseLockAsync(string entityId)
    {
        if (_locks.Remove(entityId, out var handle))
        {
            await handle.DisposeAsync();
            await hostedService.NotifyUnlockedAsync(_entityType, entityId);
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var kvp in _locks)
        {
            await kvp.Value.DisposeAsync();
            await hostedService.NotifyUnlockedAsync(_entityType, kvp.Key);
        }
        
        _locks.Clear();
    }
}