namespace AlbyOnContainers.Kernel.Persistence.HostedServices;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Options;
using Polly;

public sealed class MigrationHostedService<TDbContext>(
    IServiceProvider serviceProvider,
    IOptions<PersistenceOptions> options,
    IHostApplicationLifetime lifetime,
    ILogger<MigrationHostedService<TDbContext>> logger) : BackgroundService
    where TDbContext : DbContext
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.RunMigrationsOnStartup)
        {
            logger.LogInformation("Auto-migrations are disabled in PersistenceOptions. Skipping for {DbContext}.", typeof(TDbContext).Name);
            return;
        }

        logger.LogInformation("Applying EF Core migrations for {DbContext}...", typeof(TDbContext).Name);

        // Define a Polly v8 Resilience Pipeline for startup retries
        var retryPipeline = new ResiliencePipelineBuilder()
            .AddRetry(new()
            {
                // Number of retry attempts before giving up
                MaxRetryAttempts = 5,
                // Exponential backoff: 2s, 4s, 8s, 16s...
                Delay = TimeSpan.FromSeconds(2),
                BackoffType = DelayBackoffType.Exponential,
                // Handle any exception (usually connection or timeout issues at boot)
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
            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<TDbContext>();

            // Execute the migration wrapped in the resilience pipeline
            await retryPipeline.ExecuteAsync(async token => { await dbContext.Database.MigrateAsync(token); }, stoppingToken);

            logger.LogInformation("EF Core migrations for {DbContext} applied successfully.", typeof(TDbContext).Name);
        }
        catch (Exception ex)
        {
            // If it fails after all 5 retries, the DB is truly unreachable or there is a SQL syntax error in the migration.
            // At this point, it is safe to crash the application.
            logger.LogCritical(ex, "Migration failed for {DbContext} after multiple retries. Stopping application.", typeof(TDbContext).Name);
            lifetime.StopApplication();
            throw;
        }
    }
}