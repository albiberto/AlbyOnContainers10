using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ProductInformationManager.Infrastructure;

public class MigrationHostedService(
    IServiceProvider serviceProvider,
    ILogger<MigrationHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Avvio dell'applicazione delle migrazioni in background...");

        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ProductContext>();

        try
        {
            // Esegue la migrazione (creerà il db e la tabella schema se non esistono)
            await context.Database.MigrateAsync(stoppingToken);
            logger.LogInformation("Migrazioni applicate con successo.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Un errore è occorso durante l'applicazione delle migrazioni.");
            throw; // È importante che il servizio fallisca se il DB non è allineato
        }
    }
}
