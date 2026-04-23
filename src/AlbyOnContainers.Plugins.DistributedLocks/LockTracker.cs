using System.Collections.Concurrent;
using System.Reactive.Linq;
using AlbyOnContainers.Plugins.DistributedLocks.Model;

namespace AlbyOnContainers.Plugins.DistributedLocks;

public sealed class LockTracker<TEntity> : IDisposable
{
    private readonly ConcurrentDictionary<string, string> _locks = new();
    private readonly IDisposable _subscription;

    public LockTracker(DistributedLockHostedService hostedService)
    {
        _subscription = hostedService.Notifications
            .Where(n => n.EntityType == typeof(TEntity).Name)
            .Subscribe(notification =>
            {
                switch (notification)
                {
                    case Emit.Locked locked:
                        _locks[locked.EntityId] = locked.UserId;
                        break;
                    case Emit.Unlocked unlocked:
                        _locks.TryRemove(unlocked.EntityId, out _);
                        break;
                }
            });
    }

    public bool IsLocked(string entityId, out string? username) => _locks.TryGetValue(entityId, out username);

    public void Dispose() => _subscription.Dispose();
}