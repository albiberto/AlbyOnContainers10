using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace ProductInformationManager.Infrastructure;

public class MigrationHostedService(
    IServiceProvider serviceProvider,
    ILogger<MigrationHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("PIM migration background service starting");
        var stopwatch = Stopwatch.StartNew();

        var pipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder().Handle<Exception>(),
                MaxRetryAttempts = 10,
                DelayGenerator = args =>
                {
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, args.AttemptNumber + 1));
                    return new ValueTask<TimeSpan?>(delay);
                },
                OnRetry = args =>
                {
                    logger.LogWarning(
                        args.Outcome.Exception,
                        "PIM migration failed. Retry {RetryCount}/10 in {Seconds} seconds",
                        args.AttemptNumber + 1,
                        args.RetryDelay.TotalSeconds);
                    return default;
                }
            })
            .Build();

        await pipeline.ExecuteAsync(async ct =>
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ProductContext>();

            logger.LogInformation("PIM migration check started");

            await context.Database.MigrateAsync(ct);

            stopwatch.Stop();
            logger.LogInformation("PIM migration completed successfully in {ElapsedMs} ms", stopwatch.Elapsed.TotalMilliseconds);
        }, stoppingToken);
    }
}
