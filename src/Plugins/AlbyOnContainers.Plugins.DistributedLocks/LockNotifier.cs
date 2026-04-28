using System.Collections.Concurrent;
using Medallion.Threading;
using Microsoft.Extensions.Options;
using System.Reactive.Linq;
using AlbyOnContainers.Plugins.DistributedLocks.Model;
using AlbyOnContainers.Plugins.DistributedLocks.Options;
using Microsoft.Extensions.Logging;

namespace AlbyOnContainers.Plugins.DistributedLocks;

using HostedServices;

public sealed class LockNotifier<TEntity> : IAsyncDisposable
{
    private readonly IDistributedLockProvider _lockProvider;
    private readonly DistributedLockHostedService _hostedService;
    private readonly LockTracker<TEntity> _tracker;
    private readonly ILogger<LockNotifier<TEntity>> _logger;
    private readonly DistributedLockOptions _config;
    
    private readonly string _entityType = typeof(TEntity).Name;
    private readonly ConcurrentDictionary<string, IDistributedSynchronizationHandle> _locks = new();

    private readonly Dictionary<string, RefCountedSemaphore> _localSyncs = new();
    private readonly Lock _syncRoot = new();

    public LockNotifier(
        IDistributedLockProvider lockProvider, 
        DistributedLockHostedService hostedService, 
        LockTracker<TEntity> tracker, 
        IOptions<DistributedLockOptions> options,
        ILogger<LockNotifier<TEntity>> logger)
    {
        _lockProvider = lockProvider;
        _hostedService = hostedService;
        _tracker = tracker;
        _logger = logger;
        _config = options.Value;
    }

    public IObservable<Emit> Changes => _hostedService.Notifications.Where(n => n.EntityType == _entityType);

    public bool IsLockedBy(string entityId, out string? username) => _tracker.IsLocked(entityId, out username);

    public async Task<bool> TryAcquireLockAsync(string entityId, string username)
    {
        var refCounted = GetOrCreateSemaphore(entityId);
        await refCounted.Semaphore.WaitAsync();

        try
        {
            if (_locks.ContainsKey(entityId)) return true;

            var lockName = $"{_config.KeyPrefix}{_entityType}:{entityId}";
            var handle = await _lockProvider.TryAcquireLockAsync(lockName, _config.AcquireTimeout);

            if (handle is null) return false;
            
            if (!_locks.TryAdd(entityId, handle))
            {
                await handle.DisposeAsync();
                return true;
            }

            await _hostedService.NotifyLockedAsync(_entityType, entityId, username);
            return true;
        }
        finally
        {
            ReleaseSemaphore(entityId, refCounted);
        }
    }

    public async Task ReleaseLockAsync(string entityId)
    {
        var refCounted = GetOrCreateSemaphore(entityId);
        await refCounted.Semaphore.WaitAsync();

        try
        {
            if (_locks.Remove(entityId, out var handle))
            {
                await handle.DisposeAsync();
                await _hostedService.NotifyUnlockedAsync(_entityType, entityId);
            }
        }
        finally
        {
            ReleaseSemaphore(entityId, refCounted);
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var kvp in _locks)
        {
            await kvp.Value.DisposeAsync();
            
            try 
            { 
                await _hostedService.NotifyUnlockedAsync(_entityType, kvp.Key); 
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to broadcast unlock notification for {EntityType}:{EntityId} during disposal.", _entityType, kvp.Key);
            }
        }
        
        _locks.Clear();

        lock (_syncRoot)
        {
            foreach (var sync in _localSyncs.Values)
            {
                sync.Semaphore.Dispose();
            }
            _localSyncs.Clear();
        }
    }

    private RefCountedSemaphore GetOrCreateSemaphore(string entityId)
    {
        lock (_syncRoot)
        {
            if (!_localSyncs.TryGetValue(entityId, out var refCounted))
            {
                refCounted = new();
                _localSyncs[entityId] = refCounted;
            }
            
            refCounted.ReferenceCount++;
            return refCounted;
        }
    }

    private void ReleaseSemaphore(string entityId, RefCountedSemaphore refCounted)
    {
        refCounted.Semaphore.Release();

        lock (_syncRoot)
        {
            refCounted.ReferenceCount--;

            if (refCounted.ReferenceCount != 0) return;
            
            _localSyncs.Remove(entityId);
            refCounted.Semaphore.Dispose();
        }
    }

    private sealed class RefCountedSemaphore
    {
        public int ReferenceCount { get; set; }
        public SemaphoreSlim Semaphore { get; } = new(1, 1);
    }
}