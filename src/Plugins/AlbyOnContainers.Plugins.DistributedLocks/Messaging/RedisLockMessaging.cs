namespace AlbyOnContainers.Plugins.DistributedLocks.Messaging;

using System;
using System.Threading;
using System.Threading.Tasks;
using Abstractions;
using MessagePack;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Model;
using Options;
using StackExchange.Redis;

/// <summary>
/// Owns the Redis subscriber, MessagePack (de)serialization, the channel name,
/// and the dual-write to the active-locks hash. Stateless w.r.t. domain logic.
/// </summary>
public sealed class RedisLockMessaging : IRedisLockMessaging, IAsyncDisposable
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisLockMessaging> _logger;
    private readonly IDatabase _database;
    private readonly string _channelName;
    private readonly string _hashKey;

    private ISubscriber? _subscriber;

    public RedisLockMessaging(
        IConnectionMultiplexer redis,
        IOptions<DistributedLockOptions> options,
        ILogger<RedisLockMessaging> logger)
    {
        _redis = redis;
        _logger = logger;
        _database = _redis.GetDatabase();

        var config = options.Value;
        _channelName = config.RedisChannel!;
        _hashKey = $"{config.KeyPrefix}active-locks";
    }

    public event Action<LockEventPayload>? MessageReceived;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _subscriber = _redis.GetSubscriber();

        await _subscriber.SubscribeAsync(RedisChannel.Literal(_channelName), (_, message) =>
        {
            HandleIncomingMessage(message);
        });
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_subscriber is not null)
            await _subscriber.UnsubscribeAllAsync();
    }

    public async Task NotifyLockedAsync(string entityType, string entityId, string username)
    {
        if (_subscriber is null)
        {
            _logger.LogWarning("RedisLockMessaging is not fully started. Notification for locking {EntityType}:{EntityId} dropped.", entityType, entityId);
            return;
        }

        var hashField = $"{entityType}:{entityId}";
        var payload = new LockEventPayload(entityType, entityId, username, true);
        var bytes = MessagePackSerializer.Serialize(payload);

        // ARCHITECTURAL NOTE: Non-atomic dual-write.
        // Acceptable risk: the LockReconciliationWorker cross-checks periodically
        // and heals any crash-induced inconsistencies.
        await _database.HashSetAsync(_hashKey, hashField, username);
        await _subscriber.PublishAsync(RedisChannel.Literal(_channelName), bytes);
    }

    public async Task NotifyUnlockedAsync(string entityType, string entityId)
    {
        if (_subscriber is null)
        {
            _logger.LogWarning("RedisLockMessaging is not fully started. Notification for unlocking {EntityType}:{EntityId} dropped.", entityType, entityId);
            return;
        }

        var hashField = $"{entityType}:{entityId}";
        var payload = new LockEventPayload(entityType, entityId, null, false);
        var bytes = MessagePackSerializer.Serialize(payload);

        await _database.HashDeleteAsync(_hashKey, hashField);
        await _subscriber.PublishAsync(RedisChannel.Literal(_channelName), bytes);
    }

    private void HandleIncomingMessage(RedisValue message)
    {
        try
        {
            var bytes = (byte[]?)message;
            if (bytes is null || bytes.Length == 0) return;

            var payload = MessagePackSerializer.Deserialize<LockEventPayload>(bytes);

            // Guard against malformed messages where IsLocked=true but Username is missing
            if (payload.IsLocked && string.IsNullOrWhiteSpace(payload.Username))
            {
                _logger.LogWarning("Received 'Locked' event without a Username for {EntityType}:{EntityId}", payload.EntityType, payload.EntityId);
                return;
            }

            MessageReceived?.Invoke(payload);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process distributed lock notification.");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_subscriber is not null)
            await _subscriber.UnsubscribeAllAsync();
    }
}

