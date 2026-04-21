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
        logger.LogInformation("Avvio dell'applicazione delle migrazioni in background...");

        var pipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder().Handle<Exception>(),
                MaxRetryAttempts = 10,
                DelayGenerator = args =>
                {
                    // args.AttemptNumber parte da 0, aggiungiamo 1 per partire da 2^1 = 2 sec
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, args.AttemptNumber + 1));
                    return new ValueTask<TimeSpan?>(delay);
                },
                OnRetry = args =>
                {
                    logger.LogWarning(args.Outcome.Exception, 
                        "Errore applicando le migrazioni. Tentativo {RetryCount}/10 in corso tra {Seconds} secondi...", 
                        args.AttemptNumber + 1, args.RetryDelay.TotalSeconds);
                    return default;
                }
            })
            .Build();

        await pipeline.ExecuteAsync(async ct =>
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ProductContext>();

            logger.LogInformation("Verifica e applicazione migrazioni pendenti DB in corso...");
            
            // Esegue la migrazione (creerà il db e la tabella schema se non esistono)
            await context.Database.MigrateAsync(ct);
            
            logger.LogInformation("Migrazioni applicate con successo.");
        }, stoppingToken);
    }
}
