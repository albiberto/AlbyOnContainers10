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

    // ARCHITECTURAL: Raw subject kept for deterministic disposal.
    // Synchronized proxy used for all concurrent OnNext emissions.
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
        _keyPrefix = _config.KeyPrefix;
    }

    public IObservable<Emit> Notifications => _notifications.AsObservable();

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _subscriber = _redis.GetSubscriber();

        // 1. Subscribe to live events BEFORE recovering state to ensure no events are missed
        await _subscriber.SubscribeAsync(RedisChannel.Literal(_channelName), (_, message) =>
        {
            HandleIncomingMessage(message);
        });

        // 2. Initial state recovery on startup
        await RecoverStateFromRedisAsync(cancellationToken);

        // 3. Background self-healing loop via Rx.
        // Detects ghost locks caused by pods that crashed without releasing.
        // .Concat() ensures reconciliation runs never overlap.
        _reconciliationSubscription = Observable
            .Interval(_config.ReconciliationInterval)
            .Select(_ => Observable.FromAsync(ct => RecoverStateFromRedisAsync(ct)))
            .Concat()
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
                // FIX: Guard against malformed messages where IsLocked=true but Username is missing
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
            _logger.LogError(ex, "Failed to process distributed lock notification.");
        }
    }

    // -------------------------------------------------------------------------
    // DEDUPLICATION ENGINE (Lock-Free, CAS-based)
    // Prevents double-emit during the subscribe/recover startup gap.
    // -------------------------------------------------------------------------

    private void TryEmitLocked(string entityType, string entityId, string username)
    {
        var key = $"{entityType}:{entityId}";

        // Fast-path: atomic insertion for new locks
        if (_activeLocksDeduplicator.TryAdd(key, username))
        {
            _notifications.OnNext(new Emit.Locked(entityType, entityId, username));
            return;
        }

        // Slow-path: CAS loop for lock hijack/overwrite scenarios.
        // Only the thread that successfully swaps the value emits.
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

    // -------------------------------------------------------------------------
    // INFRASTRUCTURE METHODS
    // -------------------------------------------------------------------------

    internal async Task NotifyLockedAsync(string entityType, string entityId, string username)
    {
        if (_subscriber is null)
        {
            _logger.LogWarning("DistributedLockHostedService is not fully started. Notification for locking {EntityType}:{EntityId} dropped.", entityType, entityId);
            return;
        }

        var hashField = $"{entityType}:{entityId}";
        var payload = new LockEventPayload(entityType, entityId, username, true);
        var bytes = MessagePackSerializer.Serialize(payload);

        // ARCHITECTURAL NOTE: Non-atomic dual-write.
        // Acceptable risk: RecoverStateFromRedisAsync cross-checks on startup and
        // the background reconciliation loop handles any crash-induced inconsistencies.
        await _database.HashSetAsync(_hashKey, hashField, username);
        await _subscriber.PublishAsync(RedisChannel.Literal(_channelName), bytes);
    }

    internal async Task NotifyUnlockedAsync(string entityType, string entityId)
    {
        if (_subscriber is null)
        {
            _logger.LogWarning("DistributedLockHostedService is not fully started. Notification for unlocking {EntityType}:{EntityId} dropped.", entityType, entityId);
            return;
        }

        var hashField = $"{entityType}:{entityId}";
        var payload = new LockEventPayload(entityType, entityId, null, false);
        var bytes = MessagePackSerializer.Serialize(payload);

        await _database.HashDeleteAsync(_hashKey, hashField);
        await _subscriber.PublishAsync(RedisChannel.Literal(_channelName), bytes);
    }

    private async Task RecoverStateFromRedisAsync(CancellationToken ct)
    {
        try
        {
            var entries = await _database.HashGetAllAsync(_hashKey);
            if (entries.Length == 0) return;

            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = _config.RecoveryMaxDegreeOfParallelism,
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
        // FIX: Split(':', 2) supports complex entity IDs containing colons (e.g. URNs)
        var fieldParts = entry.Name.ToString().Split(':', 2);
        if (fieldParts.Length != 2) return;

        var entityType = fieldParts[0];
        var entityId = fieldParts[1];
        var username = entry.Value.ToString();
        var lockName = $"{_keyPrefix}{entityType}:{entityId}";

        // FIX (CRITICAL): Cross-check to detect ghost locks left by crashed pods.
        // If we can acquire the lock, it means no one holds it — the Hash entry is stale.
        var acquiredLock = await _lockProvider.TryAcquireLockAsync(lockName, TimeSpan.Zero, ct);

        if (acquiredLock is not null)
        {
            _logger.LogInformation("Reconciliation: cleaning stale ghost-lock for {LockName}.", lockName);

            await acquiredLock.DisposeAsync();

            // Broadcast unlock to all pods via pub/sub so their LockTrackers self-heal
            await NotifyUnlockedAsync(entityType, entityId);

            TryEmitUnlocked(entityType, entityId);
        }
        else
        {
            // Lock is genuinely held — re-emit to hydrate the local reactive stream.
            // Deduplication engine ensures no double-emit if pub/sub already delivered this.
            TryEmitLocked(entityType, entityId, username);
        }
    }

    public async Task StopAsync(CancellationToken ct)
    {
        // FIX: Dispose and null-out here to prevent double-dispose in DisposeAsync
        _reconciliationSubscription?.Dispose();
        _reconciliationSubscription = null;

        if (_subscriber is not null)
            await _subscriber.UnsubscribeAllAsync();
    }

    public ValueTask DisposeAsync()
    {
        // _reconciliationSubscription already disposed in StopAsync (null-safe guard kept for safety)
        _reconciliationSubscription?.Dispose();

        // Safe and deterministic disposal of the raw Subject
        _subject.OnCompleted();
        _subject.Dispose();

        return ValueTask.CompletedTask;
    }
}