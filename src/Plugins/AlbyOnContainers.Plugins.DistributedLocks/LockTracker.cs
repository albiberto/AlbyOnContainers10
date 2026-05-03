namespace AlbyOnContainers.Plugins.DistributedLocks;

using System.Collections.Concurrent;
using System.Reactive.Linq;
using Abstractions;
using Microsoft.Extensions.Logging;
using Model;

public sealed class LockTracker<TEntity> : IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, string> _locks = new();
    private readonly IDisposable _subscription;

    public LockTracker(ILockStateTracker stateTracker, ILogger<LockTracker<TEntity>> logger)
    {
        _subscription = stateTracker.Notifications
            .Where(n => n.EntityType == typeof(TEntity).Name)
            .Subscribe(
                onNext: notification =>
                {
                    switch (notification)
                    {
                        case Emit.Locked locked:
                            _locks[locked.EntityId] = locked.Username;
                            break;
                        case Emit.Unlocked unlocked:
                            _locks.TryRemove(unlocked.EntityId, out _);
                            break;
                    }
                },
                onError: ex => logger.LogError(ex, "LockTracker<{Entity}> reactive stream errored.", typeof(TEntity).Name));
    }

    public ValueTask DisposeAsync()
    {
        _subscription.Dispose();
        return ValueTask.CompletedTask;
    }

    public bool IsLocked(string entityId, out string? username) => _locks.TryGetValue(entityId, out username);
}