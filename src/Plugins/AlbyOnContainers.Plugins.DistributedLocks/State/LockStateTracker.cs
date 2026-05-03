namespace AlbyOnContainers.Plugins.DistributedLocks.State;

using System;
using System.Collections.Concurrent;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Abstractions;
using Model;

/// <summary>
/// Owns the in-memory deduplication map and the synchronized reactive subject.
/// Subscribes to <see cref="IRedisLockMessaging.MessageReceived"/> at construction.
/// Registered as Singleton.
/// </summary>
public sealed class LockStateTracker : ILockStateTracker, IDisposable
{
    // ARCHITECTURAL: Raw subject kept for deterministic disposal.
    // Synchronized proxy used for all concurrent OnNext emissions.
    private readonly Subject<Emit> _subject = new();
    private readonly ISubject<Emit> _notifications;

    private readonly ConcurrentDictionary<string, string> _activeLocksDeduplicator = new();
    private readonly IRedisLockMessaging _messaging;

    public LockStateTracker(IRedisLockMessaging messaging)
    {
        _messaging = messaging;
        _notifications = Subject.Synchronize(_subject);
        _messaging.MessageReceived += OnMessageReceived;
    }

    public IObservable<Emit> Notifications => _notifications.AsObservable();

    private void OnMessageReceived(LockEventPayload payload)
    {
        if (payload.IsLocked)
            TryEmitLocked(payload.EntityType, payload.EntityId, payload.Username!);
        else
            TryEmitUnlocked(payload.EntityType, payload.EntityId);
    }

    // -------------------------------------------------------------------------
    // DEDUPLICATION ENGINE (Lock-Free, CAS-based)
    // Prevents double-emit during the subscribe/recover startup gap.
    // -------------------------------------------------------------------------

    internal void TryEmitLocked(string entityType, string entityId, string username)
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

    internal void TryEmitUnlocked(string entityType, string entityId)
    {
        var key = $"{entityType}:{entityId}";

        if (_activeLocksDeduplicator.TryRemove(key, out _))
            _notifications.OnNext(new Emit.Unlocked(entityType, entityId));
    }

    public void Dispose()
    {
        _messaging.MessageReceived -= OnMessageReceived;
        _subject.OnCompleted();
        _subject.Dispose();
    }
}

