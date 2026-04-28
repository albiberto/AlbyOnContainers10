using System;
using System.Threading;
using System.Threading.Tasks;
using AlbyOnContainers.Kernel.Persistence.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AlbyOnContainers.Kernel.Persistence.HostedServices;

public sealed class MigrationHostedService<TDbContext>(IServiceProvider serviceProvider, IOptions<PersistenceOptions> options, ILogger<MigrationHostedService<TDbContext>> logger) : BackgroundService
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

        try
        {
            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<TDbContext>();
            
            await dbContext.Database.MigrateAsync(stoppingToken);
            
            logger.LogInformation("EF Core migrations for {DbContext} applied successfully.", typeof(TDbContext).Name);
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "An error occurred while migrating the database for {DbContext}.", typeof(TDbContext).Name);
            throw;
        }
    }
}