namespace AlbyOnContainers.Plugins.DistributedLocks.HostedServices;

using System;
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

public sealed class DistributedLockHostedService(
    IConnectionMultiplexer redis, 
    IDistributedLockProvider lockProvider, 
    IOptions<DistributedLockOptions> options,
    ILogger<DistributedLockHostedService> logger) : IHostedService, IAsyncDisposable
{
    private ISubscriber? _subscriber;
    private readonly IDatabase _database = redis.GetDatabase();
    
    private readonly ISubject<Emit> _notifications = Subject.Synchronize(new Subject<Emit>());
    
    private readonly string _channelName = options.Value.RedisChannel!;
    private readonly string _hashKey = $"{options.Value.KeyPrefix}active-locks";
    private readonly string _keyPrefix = options.Value.KeyPrefix ?? string.Empty;

    public IObservable<Emit> Notifications => _notifications.AsObservable();

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _subscriber = redis.GetSubscriber();
        
        await _subscriber.SubscribeAsync(RedisChannel.Literal(_channelName), (_, message) =>
        {
            try
            {
                var bytes = (byte[]?)message;
                if (bytes is null || bytes.Length == 0) return;

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
            
            if (entries.Length == 0) return;

            var recoveredCount = 0;

            var parallelOptions = new ParallelOptions 
            { 
                MaxDegreeOfParallelism = Environment.ProcessorCount * 2, // Conservative but fast limit
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
                logger.LogInformation("Successfully recovered {Count} active distributed locks from Redis concurrently.", recoveredCount);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogCritical(ex, "FATAL: Failed to recover distributed locks state from Redis during startup.");
        }
    }

    private async ValueTask<bool> CrossCheckAndRecoverAsync(HashEntry entry, CancellationToken ct)
    {
        var fieldParts = entry.Name.ToString().Split(':', 2);
        if (fieldParts.Length != 2)
        {
            logger.LogWarning("Malformed hash entry found during recovery: {Key}", entry.Name);
            return false;
        }

        var entityType = fieldParts[0];
        var entityId = fieldParts[1];
        var username = entry.Value.ToString();

        var lockName = $"{_keyPrefix}{entityType}:{entityId}";

        var acquiredLock = await lockProvider.TryAcquireLockAsync(lockName, TimeSpan.Zero, ct);
        
        if (acquiredLock is not null)
        {
            logger.LogInformation("Detected stale ghost-lock state for {LockName}. Cleaning up.", lockName);
            
            await acquiredLock.DisposeAsync();
            await _database.HashDeleteAsync(_hashKey, entry.Name);
            return false;
        }

        // Emit is safely thread-synchronized thanks to Subject.Synchronize()
        _notifications.OnNext(new Emit.Locked(entityType, entityId, username));
        return true;
    }

    public ValueTask DisposeAsync()
    {
        _notifications.OnCompleted();
        if (_notifications is IDisposable disposable) disposable.Dispose();
        
        return ValueTask.CompletedTask;
    }
}