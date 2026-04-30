namespace AlbyOnContainers.Plugins.DistributedLocks.HostedServices;

using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Medallion.Threading; // Mandatory for cross-checking actual lock state
using Model;
using Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using MessagePack;

public sealed class DistributedLockHostedService(
    IConnectionMultiplexer redis, 
    IDistributedLockProvider lockProvider, // Injected for ghost-lock detection
    IOptions<DistributedLockOptions> options,
    ILogger<DistributedLockHostedService> logger) : IHostedService, IAsyncDisposable
{
    private ISubscriber? _subscriber;
    private readonly IDatabase _database = redis.GetDatabase();
    private readonly Subject<Emit> _notifications = new();
    
    // Extracted configurations for cleaner access
    private readonly string _channelName = options.Value.RedisChannel!;
    private readonly string _hashKey = $"{options.Value.KeyPrefix}active-locks";
    private readonly string _keyPrefix = options.Value.KeyPrefix ?? string.Empty;

    public IObservable<Emit> Notifications => _notifications.AsObservable();

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _subscriber = redis.GetSubscriber();
        
        // 1. Subscribe to live events BEFORE recovering state to ensure no events are missed
        await _subscriber.SubscribeAsync(RedisChannel.Literal(_channelName), (_, message) =>
        {
            try
            {
                var bytes = (byte[]?)message;
                if (bytes is null || bytes.Length == 0) return;

                // Architectural Standard: Strictly MessagePack for binary caching/messaging
                var payload = MessagePackSerializer.Deserialize<LockEventPayload>(bytes);

                if (payload.IsLocked)
                {
                    if (payload.Username is null)
                    {
                        logger.LogWarning("Received 'Locked' event without a Username for {EntityType}:{EntityId}", payload.EntityType, payload.EntityId);
                        return;
                    }
                    _notifications.OnNext(new Emit.Locked(payload.EntityType, payload.EntityId, payload.Username));
                }
                else
                {
                    _notifications.OnNext(new Emit.Unlocked(payload.EntityType, payload.EntityId));
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to process distributed lock notification from Redis channel {Channel}", _channelName);
            }
        });

        // 2. Hydrate local state from the Redis Hash Read-Model
        await RecoverStateFromRedisAsync(cancellationToken);
    }

    internal async Task NotifyLockedAsync(string entityType, string entityId, string username)
    {
        if (_subscriber is null)
        {
            logger.LogWarning("DistributedLockHostedService is not fully started. Notification for locking {EntityType}:{EntityId} dropped.", entityType, entityId);
            return;
        }

        var hashField = $"{entityType}:{entityId}";
        var payload = new LockEventPayload(entityType, entityId, username, true);
        var bytes = MessagePackSerializer.Serialize(payload);
        
        // ARCHITECTURAL NOTE: Non-atomic dual-write.
        // Acceptable risk because the RecoverStateFromRedisAsync cross-check on reboot handles crash inconsistencies.
        await _database.HashSetAsync(_hashKey, hashField, username);
        await _subscriber.PublishAsync(RedisChannel.Literal(_channelName), bytes);
    }

    internal async Task NotifyUnlockedAsync(string entityType, string entityId)
    {
        if (_subscriber is null)
        {
            logger.LogWarning("DistributedLockHostedService is not fully started. Notification for unlocking {EntityType}:{EntityId} dropped.", entityType, entityId);
            return;
        }

        var hashField = $"{entityType}:{entityId}";
        var payload = new LockEventPayload(entityType, entityId, null, false);
        var bytes = MessagePackSerializer.Serialize(payload);
        
        // ARCHITECTURAL NOTE: Non-atomic dual-write.
        await _database.HashDeleteAsync(_hashKey, hashField);
        await _subscriber.PublishAsync(RedisChannel.Literal(_channelName), bytes);
    }

    public async Task StopAsync(CancellationToken ct)
    {
        if (_subscriber is not null) 
        {
            await _subscriber.UnsubscribeAllAsync();
        }
    }
    
    private async Task RecoverStateFromRedisAsync(CancellationToken cancellationToken)
    {
        try
        {
            var entries = await _database.HashGetAllAsync(_hashKey);
            var recoveredCount = 0;

            foreach (var entry in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // FIX: Limit split to 2 parts to support complex IDs (e.g., URNs containing colons)
                var fieldParts = entry.Name.ToString().Split(':', 2);
                if (fieldParts.Length != 2)
                {
                    logger.LogWarning("Malformed hash entry found during recovery: {Key}", entry.Name);
                    continue;
                }

                var entityType = fieldParts[0];
                var entityId = fieldParts[1];
                var username = entry.Value.ToString();

                var lockName = $"{_keyPrefix}{entityType}:{entityId}";

                // FIX (CRITICAL): Cross-check to prevent permanent "Ghost Locks" from dead pods
                // TimeSpan.Zero ensures a non-blocking immediate check
                var acquiredLock = await lockProvider.TryAcquireLockAsync(lockName, TimeSpan.Zero, cancellationToken);
                
                if (acquiredLock is not null)
                {
                    logger.LogInformation("Detected stale ghost-lock state for {LockName}. Cleaning up.", lockName);
                    
                    await acquiredLock.DisposeAsync(); // Instantly release
                    await _database.HashDeleteAsync(_hashKey, entry.Name);
                    continue;
                }

                // Lock is genuinely held by someone, re-emit to UI
                recoveredCount++;
                _notifications.OnNext(new Emit.Locked(entityType, entityId, username));
            }
            
            if (recoveredCount > 0)
            {
                logger.LogInformation("Successfully recovered {Count} active distributed locks from Redis.", recoveredCount);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // We log a Critical error but do not throw, allowing the pod to start 
            // and eventually reach consistency via new Pub/Sub events.
            logger.LogCritical(ex, "FATAL: Failed to recover distributed locks state from Redis during startup.");
        }
    }

    public ValueTask DisposeAsync()
    {
        // Graceful termination of the Rx stream (safe as per previous architectural review)
        _notifications.OnCompleted();
        _notifications.Dispose();
        
        return ValueTask.CompletedTask;
    }
}