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
    private IDisposable? _reconciliationSubscription;
    
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
        
        // 1. Live Events Subscription
        await _subscriber.SubscribeAsync(RedisChannel.Literal(_channelName), (_, message) =>
        {
            HandleIncomingMessage(message);
        });

        // 2. Initial State Recovery (Sequential startup)
        await RecoverStateFromRedisAsync(cancellationToken);

        // 3. BACKGROUND SELF-HEALING (Rx.NET Reconciliation Loop)
        // We start a periodic task that runs every N minutes to fix drift between Hash and TTL Locks.
        _reconciliationSubscription = Observable
            .Interval(_config.ReconciliationInterval)
            .Select(_ => Observable.FromAsync(ct => RecoverStateFromRedisAsync(ct)))
            .Concat() // Ensures reconciliation runs don't overlap
            .Subscribe(
                _ => _logger.LogDebug("Background distributed lock reconciliation completed."),
                ex => _logger.LogError(ex, "Error in background reconciliation loop."));
    }

    private void HandleIncomingMessage(RedisValue message)
    {
        try
        {
            var bytes = (byte[]?)message;
            if (bytes is null || bytes.Length == 0) return;

            var payload = MessagePackSerializer.Deserialize<LockEventPayload>(bytes);

            if (payload.IsLocked)
            {
                TryEmitLocked(payload.EntityType, payload.EntityId, payload.Username!);
            }
            else
            {
                TryEmitUnlocked(payload.EntityType, payload.EntityId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process distributed lock notification.");
        }
    }

    private void TryEmitLocked(string entityType, string entityId, string username)
    {
        var key = $"{entityType}:{entityId}";

        if (_activeLocksDeduplicator.TryAdd(key, username))
        {
            _notifications.OnNext(new Emit.Locked(entityType, entityId, username));
            return;
        }

        while (true)
        {
            if (!_activeLocksDeduplicator.TryGetValue(key, out var currentUsername)) break;
            if (currentUsername.Equals(username, StringComparison.Ordinal)) break;

            if (!_activeLocksDeduplicator.TryUpdate(key, username, currentUsername)) continue;
            
            _notifications.OnNext(new Emit.Locked(entityType, entityId, username));
            break;
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

    private async Task RecoverStateFromRedisAsync(CancellationToken ct)
    {
        try
        {
            var entries = await _database.HashGetAllAsync(_hashKey);
            if (entries.Length == 0) return;

            var parallelOptions = new ParallelOptions 
            { 
                MaxDegreeOfParallelism = _config.RecoveryMaxDegreeOfParallelism > 0 ? _config.RecoveryMaxDegreeOfParallelism : 32,
                CancellationToken = ct 
            };

            await Parallel.ForEachAsync(entries, parallelOptions, async (entry, innerCt) => 
            {
                await CrossCheckAndRecoverAsync(entry, innerCt);
            });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Distributed lock state recovery/reconciliation failed.");
        }
    }

    private async ValueTask CrossCheckAndRecoverAsync(HashEntry entry, CancellationToken ct)
    {
        var fieldParts = entry.Name.ToString().Split(':', 2);
        if (fieldParts.Length != 2) return;

        var entityType = fieldParts[0];
        var entityId = fieldParts[1];
        var username = entry.Value.ToString();
        var lockName = $"{_keyPrefix}{entityType}:{entityId}";

        var acquiredLock = await _lockProvider.TryAcquireLockAsync(lockName, TimeSpan.Zero, ct);
        
        if (acquiredLock is not null)
        {
            _logger.LogInformation("Reconciliation: cleaning stale ghost-lock for {LockName}.", lockName);
            
            await acquiredLock.DisposeAsync();
            
            await NotifyUnlockedAsync(entityType, entityId);
            
            TryEmitUnlocked(entityType, entityId);
        }
        else
        {
            TryEmitLocked(entityType, entityId, username);
        }
    }

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

    public async Task StopAsync(CancellationToken ct)
    {
        _reconciliationSubscription?.Dispose();
        if (_subscriber is not null) await _subscriber.UnsubscribeAllAsync();
    }

    public ValueTask DisposeAsync()
    {
        _reconciliationSubscription?.Dispose();
        _subject.OnCompleted();
        _subject.Dispose();
        return ValueTask.CompletedTask;
    }
}