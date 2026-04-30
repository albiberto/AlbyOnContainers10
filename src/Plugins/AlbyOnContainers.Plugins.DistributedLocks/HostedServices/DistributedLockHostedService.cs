namespace AlbyOnContainers.Plugins.DistributedLocks.HostedServices;

using System;
using System.Collections.Concurrent;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Medallion.Threading;
using Model;
using Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using MessagePack;

public sealed class DistributedLockHostedService : IHostedService, IAsyncDisposable
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IDistributedLockProvider _lockProvider;
    private readonly ILogger<DistributedLockHostedService> _logger;
    private readonly IDatabase _database;
    private readonly DistributedLockOptions _config;
    
    private ISubscriber? _subscriber;
    
    // ARCHITECTURAL FIX: Proper Rx Lifecycle Management.
    // The raw subject is kept for deterministic disposal, while the synchronized proxy 
    // is exposed and used internally for concurrent OnNext emissions.
    private readonly Subject<Emit> _subject = new();
    private readonly ISubject<Emit> _notifications;
    
    private readonly ConcurrentDictionary<string, string> _activeLocksDeduplicator = new();

    private readonly string _channelName;
    private readonly string _hashKey;
    private readonly string _keyPrefix;

    public DistributedLockHostedService(
        IConnectionMultiplexer redis, 
        IDistributedLockProvider lockProvider, 
        IOptions<DistributedLockOptions> options,
        ILogger<DistributedLockHostedService> logger)
    {
        _redis = redis;
        _lockProvider = lockProvider;
        _config = options.Value;
        _logger = logger;
        _database = _redis.GetDatabase();

        _notifications = Subject.Synchronize(_subject);
        
        _channelName = _config.RedisChannel!;
        _hashKey = $"{_config.KeyPrefix}active-locks";
        _keyPrefix = _config.KeyPrefix ?? string.Empty;
    }

    public IObservable<Emit> Notifications => _notifications.AsObservable();

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _subscriber = _redis.GetSubscriber();
        
        await _subscriber.SubscribeAsync(RedisChannel.Literal(_channelName), (_, message) =>
        {
            try
            {
                var bytes = (byte[]?)message;
                if (bytes is null || bytes.Length == 0) return;

                var payload = MessagePackSerializer.Deserialize<LockEventPayload>(bytes);

                if (payload.IsLocked)
                {
                    if (string.IsNullOrWhiteSpace(payload.Username))
                    {
                        _logger.LogWarning("Received 'Locked' event without a Username for {EntityType}:{EntityId}", payload.EntityType, payload.EntityId);
                        return;
                    }
                    
                    TryEmitLocked(payload.EntityType, payload.EntityId, payload.Username);
                }
                else
                {
                    TryEmitUnlocked(payload.EntityType, payload.EntityId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process distributed lock notification from Redis channel {Channel}", _channelName);
            }
        });

        await RecoverStateFromRedisAsync(cancellationToken);
    }

    // -------------------------------------------------------------------------
    // DEDUPLICATION ENGINE (Lock-Free Thread-Safe CAS)
    // -------------------------------------------------------------------------
    
    private void TryEmitLocked(string entityType, string entityId, string username)
    {
        var key = $"{entityType}:{entityId}";

        // Fast-path: atomic insertion for newly acquired locks
        if (_activeLocksDeduplicator.TryAdd(key, username))
        {
            _notifications.OnNext(new Emit.Locked(entityType, entityId, username));
            return;
        }

        // Slow-path: Compare-And-Swap (CAS) loop for lock hijacks/overwrites
        // This guarantees atomicity without locking, even under extreme contention
        while (true)
        {
            if (!_activeLocksDeduplicator.TryGetValue(key, out var currentUsername))
                break; // Concurrently removed by an Unlock event. Bail out safely.

            if (currentUsername.Equals(username, StringComparison.Ordinal))
                break; // No state change detected. Discard duplicate.

            if (_activeLocksDeduplicator.TryUpdate(key, username, currentUsername))
            {
                // Only the thread that successfully swaps the state emits the notification
                _notifications.OnNext(new Emit.Locked(entityType, entityId, username));
                break;
            }
        }
    }

    private void TryEmitUnlocked(string entityType, string entityId)
    {
        var key = $"{entityType}:{entityId}";
        
        if (_activeLocksDeduplicator.TryRemove(key, out _))
        {
            _notifications.OnNext(new Emit.Unlocked(entityType, entityId));
        }
    }

    // -------------------------------------------------------------------------
    // INFRASTRUCTURE METHODS
    // -------------------------------------------------------------------------

    internal async Task NotifyLockedAsync(string entityType, string entityId, string username)
    {
        if (_subscriber is null) return;

        var hashField = $"{entityType}:{entityId}";
        var payload = new LockEventPayload(entityType, entityId, username, true);
        var bytes = MessagePackSerializer.Serialize(payload);
        
        await _database.HashSetAsync(_hashKey, hashField, username);
        await _subscriber.PublishAsync(RedisChannel.Literal(_channelName), bytes);
    }

    internal async Task NotifyUnlockedAsync(string entityType, string entityId)
    {
        if (_subscriber is null) return;

        var hashField = $"{entityType}:{entityId}";
        var payload = new LockEventPayload(entityType, entityId, null, false);
        var bytes = MessagePackSerializer.Serialize(payload);
        
        await _database.HashDeleteAsync(_hashKey, hashField);
        await _subscriber.PublishAsync(RedisChannel.Literal(_channelName), bytes);
    }
    
    private async Task RecoverStateFromRedisAsync(CancellationToken cancellationToken)
    {
        try
        {
            var entries = await _database.HashGetAllAsync(_hashKey);
            if (entries.Length == 0) return;

            var recoveredCount = 0;
            
            var degreeOfParallelism = _config.RecoveryMaxDegreeOfParallelism > 0 
                ? _config.RecoveryMaxDegreeOfParallelism 
                : 32;

            var parallelOptions = new ParallelOptions 
            { 
                MaxDegreeOfParallelism = degreeOfParallelism,
                CancellationToken = cancellationToken 
            };

            await Parallel.ForEachAsync(entries, parallelOptions, async (entry, ct) => 
            {
                var wasRecovered = await CrossCheckAndRecoverAsync(entry, ct);
                if (wasRecovered)
                {
                    Interlocked.Increment(ref recoveredCount);
                }
            });

            if (recoveredCount > 0)
            {
                _logger.LogInformation("Successfully recovered {Count} active distributed locks from Redis concurrently.", recoveredCount);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogCritical(ex, "FATAL: Failed to recover distributed locks state from Redis during startup.");
        }
    }

    private async ValueTask<bool> CrossCheckAndRecoverAsync(HashEntry entry, CancellationToken ct)
    {
        var fieldParts = entry.Name.ToString().Split(':', 2);
        if (fieldParts.Length != 2) return false;

        var entityType = fieldParts[0];
        var entityId = fieldParts[1];
        var username = entry.Value.ToString();
        var lockName = $"{_keyPrefix}{entityType}:{entityId}";

        var acquiredLock = await _lockProvider.TryAcquireLockAsync(lockName, TimeSpan.Zero, ct);
        if (acquiredLock is not null)
        {
            _logger.LogInformation("Detected stale ghost-lock state for {LockName}. Cleaning up.", lockName);
            await acquiredLock.DisposeAsync();
            await _database.HashDeleteAsync(_hashKey, entry.Name);
            return false;
        }

        TryEmitLocked(entityType, entityId, username);
        return true;
    }

    public async Task StopAsync(CancellationToken ct)
    {
        if (_subscriber is not null) 
        {
            await _subscriber.UnsubscribeAllAsync();
        }
    }

    public ValueTask DisposeAsync()
    {
        // Safe and deterministic disposal of the raw Subject
        _subject.OnCompleted();
        _subject.Dispose();
        
        return ValueTask.CompletedTask;
    }
}