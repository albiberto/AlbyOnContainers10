namespace AlbyOnContainers.Plugins.DistributedLocks.HostedServices;

using System;
using System.Threading;
using System.Threading.Tasks;
using Abstractions;
using Medallion.Threading;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Options;
using StackExchange.Redis;

/// <summary>
/// Periodic self-healing for ghost-locks: locks recorded in the active-locks Hash
/// but no longer held by any process (e.g. crashed pods that didn't release).
/// </summary>
public sealed class LockReconciliationWorker : BackgroundService
{
    private readonly IDatabase _database;
    private readonly IDistributedLockProvider _lockProvider;
    private readonly IRedisLockMessaging _messaging;
    private readonly ILogger<LockReconciliationWorker> _logger;
    private readonly DistributedLockOptions _config;

    private readonly string _hashKey;
    private readonly string _keyPrefix;

    public LockReconciliationWorker(
        IConnectionMultiplexer redis,
        IDistributedLockProvider lockProvider,
        IRedisLockMessaging messaging,
        IOptions<DistributedLockOptions> options,
        ILogger<LockReconciliationWorker> logger)
    {
        _database = redis.GetDatabase();
        _lockProvider = lockProvider;
        _messaging = messaging;
        _logger = logger;
        _config = options.Value;

        _keyPrefix = _config.KeyPrefix;
        _hashKey = $"{_keyPrefix}active-locks";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Initial recovery on startup
        await RecoverStateFromRedisAsync(stoppingToken);

        using var timer = new PeriodicTimer(_config.ReconciliationInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RecoverStateFromRedisAsync(stoppingToken);
                _logger.LogDebug("Background distributed lock reconciliation completed.");
            }
        }
        catch (OperationCanceledException)
        {
            // graceful shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in background reconciliation loop.");
        }
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
        // Split(':', 2) supports complex entity IDs containing colons (e.g. URNs)
        var fieldParts = entry.Name.ToString().Split(':', 2);
        if (fieldParts.Length != 2) return;

        var entityType = fieldParts[0];
        var entityId = fieldParts[1];
        var lockName = $"{_keyPrefix}{entityType}:{entityId}";

        // CRITICAL: Cross-check to detect ghost locks left by crashed pods.
        // If we can acquire the lock, it means no one holds it — the Hash entry is stale.
        var acquiredLock = await _lockProvider.TryAcquireLockAsync(lockName, TimeSpan.Zero, ct);

        if (acquiredLock is null) return;

        _logger.LogInformation("Reconciliation: cleaning stale ghost-lock for {LockName}.", lockName);

        await acquiredLock.DisposeAsync();

        // Broadcast unlock to all pods via pub/sub so their LockStateTrackers self-heal.
        // The local tracker will receive its own broadcast through the messaging round-trip.
        await _messaging.NotifyUnlockedAsync(entityType, entityId);
    }
}

