namespace AlbyOnContainers.Plugins.DistributedLocks.Abstractions;

using System;
using Model;

/// <summary>
/// Local in-memory tracker of active distributed locks plus the reactive stream
/// of <see cref="Emit"/> notifications. Singleton, thread-safe, deduplicated.
/// </summary>
public interface ILockStateTracker
{
    IObservable<Emit> Notifications { get; }
}

