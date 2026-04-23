using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text.Json;
using AlbyOnContainers.Plugins.DistributedLocks.Model;
using AlbyOnContainers.Plugins.DistributedLocks.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace AlbyOnContainers.Plugins.DistributedLocks;

public sealed class DistributedLockHostedService(IConnectionMultiplexer redis, IOptions<DistributedLockOptions> options) : IHostedService, IAsyncDisposable
{
    private ISubscriber? _subscriber;
    private readonly Subject<Emit> _notifications = new();
    private readonly string _channelName = options.Value.RedisChannel!;

    public IObservable<Emit> Notifications => _notifications.AsObservable();

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _subscriber = redis.GetSubscriber();
        
        _subscriber.Subscribe(RedisChannel.Literal(_channelName), (_, message) =>
        {
            var bytes = (byte[]?)message;
            if (bytes is null || bytes.Length == 0) return;

            var payload = JsonSerializer.Deserialize<LockEventPayload>(bytes);
            if (payload is null) return;

            if (payload.IsLocked)
                _notifications.OnNext(new Emit.Locked(payload.EntityType, payload.EntityId, payload.UserId));
            else
                _notifications.OnNext(new Emit.Unlocked(payload.EntityType, payload.EntityId));
        });

        return Task.CompletedTask;
    }

    public async Task NotifyLockedAsync(string entityType, string entityId, string userId)
    {
        var payload = new LockEventPayload(entityType, entityId, userId, true);
        
        var utf8Bytes = JsonSerializer.SerializeToUtf8Bytes(payload);
        
        await redis.GetSubscriber().PublishAsync(RedisChannel.Literal(_channelName), utf8Bytes);
    }

    public async Task NotifyUnlockedAsync(string entityType, string entityId)
    {
        var payload = new LockEventPayload(entityType, entityId, string.Empty, false);
        
        var utf8Bytes = JsonSerializer.SerializeToUtf8Bytes(payload);
        
        await redis.GetSubscriber().PublishAsync(RedisChannel.Literal(_channelName), utf8Bytes);
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        if (_subscriber is not null) await _subscriber.UnsubscribeAllAsync();
        _notifications.Dispose();
    }
}