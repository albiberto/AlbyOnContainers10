namespace AlbyOnContainers.Kernel.Persistence.HostedServices;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Options;
using Resilience.Enums;
using Medallion.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;

public sealed partial class MigrationHostedService<TDbContext>(
    IServiceScopeFactory scopeFactory,
    IOptions<PersistenceOptions> options,
    IHostApplicationLifetime lifetime,
    IDistributedLockProvider lockProvider,
    ILogger<MigrationHostedService<TDbContext>> logger,
    [FromKeyedServices(ResilienceKey.Database)] ResiliencePipeline pipeline)
    : IHostedService where TDbContext : DbContext
{
    private readonly PersistenceOptions _options = options.Value;

    // Architectural Note: Consider moving this into PersistenceOptions 
    // to fully embrace the Dual-Method Options Pattern and Fail-Fast principles.
    private static readonly TimeSpan LockTimeout = TimeSpan.FromSeconds(30);

    private readonly record struct MigrationExecutionState(
        IServiceScopeFactory ScopeFactory,
        IDistributedLockProvider LockProvider,
        string LockName,
        string DbName,
        MigrationHostedService<TDbContext> ServiceInstance);

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.RunMigrationsOnStartup) return;

        var dbName = typeof(TDbContext).Name;
        var lockName = $"ef_migration_lock_{dbName.ToLowerInvariant()}";

        var executionState = new MigrationExecutionState(
            scopeFactory,
            lockProvider,
            lockName,
            dbName,
            this);

        try
        {
            await pipeline.ExecuteAsync(static async (state, token) =>
            {
                await using var scope = state.ScopeFactory.CreateAsyncScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<TDbContext>();

                await using var distributedLock = await state.LockProvider.AcquireLockAsync(
                    state.LockName,
                    timeout: LockTimeout,
                    cancellationToken: token);

                var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync(token);
                var migrationsToApply = pendingMigrations.ToList();

                if (migrationsToApply.Count != 0)
                {
                    state.ServiceInstance.LogApplyingMigrations(migrationsToApply.Count, state.DbName);
                    await dbContext.Database.MigrateAsync(token);
                }
            }, executionState, cancellationToken); // Removed redundant ConfigureAwait(false)
        }
        catch (Exception ex)
        {
            LogMigrationCriticalFailure(ex, dbName);
            lifetime.StopApplication();
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}