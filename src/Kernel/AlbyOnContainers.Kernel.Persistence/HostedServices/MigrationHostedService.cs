namespace AlbyOnContainers.Kernel.Persistence.HostedServices;

using System.Diagnostics;
using Medallion.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Options;
using Polly;

/// <summary>
///     Applies pending EF Core migrations on startup, coordinated across replicas
///     via a distributed lock. Fail-fast on errors so the orchestrator restarts the pod.
/// </summary>
public sealed partial class MigrationHostedService<TDbContext>(
    IServiceScopeFactory scopeFactory,
    IHostApplicationLifetime lifetime,
    IDistributedLockProvider lockProvider,
    [FromKeyedServices(ResilienceKey.Database)]
    ResiliencePipeline pipeline,
    IOptions<PersistenceOptions> options,
    ILogger<MigrationHostedService<TDbContext>> logger)
    : IHostedService
    where TDbContext : DbContext
{
    private readonly string _dbContextName = typeof(TDbContext).Name;
    private readonly PersistenceOptions _options = options.Value;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.RunMigrationsOnStartup)
        {
            LogMigrationsDisabled(_dbContextName);
            return;
        }

        using var activity = MigrationTelemetry.ActivitySource.StartActivity($"Migrate {_dbContextName}");
        activity?.SetTag("db.system.name", _dbContextName);

        try
        {
            await pipeline.ExecuteAsync(static (state, ct) => state.RunMigrationAsync(ct), this, cancellationToken);
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (OperationCanceledException)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "Cancelled by Host Orchestrator");
            LogMigrationCancelled(_dbContextName);
            throw;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            LogMigrationCriticalFailure(ex, _dbContextName);

            // Fail-fast: Only signal StopApplication on genuine, unexpected faults
            // so the orchestrator can clean up the dead pod and attempt a restart.
            lifetime.StopApplication();
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async ValueTask RunMigrationAsync(CancellationToken ct)
    {
        var lockName = $"ef_migration_lock_{_dbContextName.ToLowerInvariant()}";

        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TDbContext>();

        await using var distributedLock = await lockProvider.TryAcquireLockAsync(lockName, _options.LockTimeout, ct) ?? throw new TimeoutException($"Timed out after {_options.LockTimeout} waiting for migration lock '{lockName}'.");

        var pending = (await dbContext.Database.GetPendingMigrationsAsync(ct)).ToList();

        if (pending.Count == 0)
        {
            LogNoPendingMigrations(_dbContextName);
            return;
        }

        LogApplyingMigrations(pending.Count, _dbContextName);

        await dbContext.Database.MigrateAsync(ct);
        LogMigrationsApplied(pending.Count, _dbContextName);
    }
}