namespace AlbyOnContainers.Plugins.DistributedLocks.HostedServices;

using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text.Json;
using Model;
using Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

public sealed class DistributedLockHostedService(
    IConnectionMultiplexer redis, 
    IOptions<DistributedLockOptions> options,
    ILogger<DistributedLockHostedService> logger) : IHostedService, IAsyncDisposable
{
    private ISubscriber? _subscriber;
    private readonly IDatabase _database = redis.GetDatabase();
    private readonly Subject<Emit> _notifications = new();
    private readonly string _channelName = options.Value.RedisChannel!;
    private readonly string _hashKey = $"{options.Value.KeyPrefix}active-locks";

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

                var payload = MessagePack.MessagePackSerializer.Deserialize<LockEventPayload>(bytes);

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
        await RecoverStateFromRedisAsync();
    }
    public async Task NotifyLockedAsync(string entityType, string entityId, string userId)
    {
        if (_subscriber is null)
        {
            logger.LogWarning("DistributedLockHostedService is not fully started. Notification for locking {EntityType}:{EntityId} dropped.", entityType, entityId);
            return;
        }

        var hashField = $"{entityType}:{entityId}";
        var payload = new LockEventPayload(entityType, entityId, userId, true);
        var bytes = MessagePack.MessagePackSerializer.Serialize(payload);
        
        // Dual-write: persist the state, then broadcast
        await _database.HashSetAsync(_hashKey, hashField, userId);
        await _subscriber.PublishAsync(RedisChannel.Literal(_channelName), bytes);
    }

    public async Task NotifyUnlockedAsync(string entityType, string entityId)
    {
        if (_subscriber is null)
        {
            logger.LogWarning("DistributedLockHostedService is not fully started. Notification for unlocking {EntityType}:{EntityId} dropped.", entityType, entityId);
            return;
        }

        var hashField = $"{entityType}:{entityId}";
        var payload = new LockEventPayload(entityType, entityId, null, false);
        var bytes = MessagePack.MessagePackSerializer.Serialize(payload);
        
        // Dual-write: remove the state, then broadcast
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
    
    private async Task RecoverStateFromRedisAsync()
    {
        try
        {
            var entries = await _database.HashGetAllAsync(_hashKey);

            foreach (var entry in entries)
            {
                var fieldParts = entry.Name.ToString().Split(':');
                if (fieldParts.Length != 2) continue;

                var entityType = fieldParts[0];
                var entityId = fieldParts[1];
                var userId = entry.Value.ToString();

                // Re-emit the recovered state to the local reactive stream
                _notifications.OnNext(new Emit.Locked(entityType, entityId, userId));
            }
            
            if (entries.Length > 0)
            {
                logger.LogInformation("Recovered {Count} active distributed locks from Redis Hash.", entries.Length);
            }
        }
        catch (Exception ex)
        {
            // We log a Critical error but do not throw, allowing the pod to start 
            // and eventually reach consistency via new Pub/Sub events.
            logger.LogCritical(ex, "FATAL: Failed to recover distributed locks state from Redis during startup.");
        }
    }

    public ValueTask DisposeAsync()
    {
        _notifications.OnCompleted();
        _notifications.Dispose();
        
        return ValueTask.CompletedTask;
    }
}