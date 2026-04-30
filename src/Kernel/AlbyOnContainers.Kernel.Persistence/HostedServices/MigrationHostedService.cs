namespace AlbyOnContainers.Kernel.Persistence.HostedServices;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Options;
using Polly;

/// <summary>
/// Applies EF Core migrations during application bootstrap.
/// Implemented as a synchronous-blocking <see cref="IHostedService"/> (not <see cref="BackgroundService"/>)
/// so that the host does NOT start serving requests until migrations have been applied.
/// </summary>
public sealed class MigrationHostedService<TDbContext>(
    IServiceProvider serviceProvider,
    IOptions<PersistenceOptions> options,
    IHostApplicationLifetime lifetime,
    ILogger<MigrationHostedService<TDbContext>> logger) : IHostedService
    where TDbContext : DbContext
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.RunMigrationsOnStartup)
        {
            logger.LogInformation("Auto-migrations are disabled in PersistenceOptions. Skipping for {DbContext}.", typeof(TDbContext).Name);
            return;
        }

        logger.LogInformation("Applying EF Core migrations for {DbContext}...", typeof(TDbContext).Name);

        // Polly v8 resilience pipeline: 5 retries with exponential backoff (2s, 4s, 8s, 16s, 32s).
        var retryPipeline = new ResiliencePipelineBuilder()
            .AddRetry(new()
            {
                MaxRetryAttempts = 5,
                Delay = TimeSpan.FromSeconds(2),
                BackoffType = DelayBackoffType.Exponential,
                ShouldHandle = new PredicateBuilder().Handle<Exception>(),
                OnRetry = args =>
                {
                    logger.LogWarning(
                        args.Outcome.Exception,
                        "Migration attempt {AttemptNumber} for {DbContext} failed. Retrying in {RetryDelay}...",
                        args.AttemptNumber + 1,
                        typeof(TDbContext).Name,
                        args.RetryDelay);

                    return default;
                }
            })
            .Build();

        try
        {
            await retryPipeline.ExecuteAsync(static async (sp, token) =>
            {
                await using var scope = sp.CreateAsyncScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<TDbContext>();
                await dbContext.Database.MigrateAsync(token);
            }, serviceProvider, cancellationToken).ConfigureAwait(false);

            logger.LogInformation("EF Core migrations for {DbContext} applied successfully.", typeof(TDbContext).Name);
        }
        catch (Exception ex)
        {
            // After all retries: DB is unreachable or migration is broken. Crash the app on purpose.
            logger.LogCritical(ex, "Migration failed for {DbContext} after multiple retries. Stopping application.", typeof(TDbContext).Name);
            lifetime.StopApplication();
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}