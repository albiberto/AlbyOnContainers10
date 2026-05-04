namespace AlbyOnContainers.Kernel.Persistence.HostedServices;

using System;
using Microsoft.Extensions.Logging;

public sealed partial class MigrationHostedService<TDbContext>
{
    [LoggerMessage(
        EventId = 200, 
        Level = LogLevel.Information, 
        Message = "Applying {Count} pending migrations for {DbContextName}...")]
    private partial void LogApplyingMigrations(int count, string dbContextName);

    [LoggerMessage(
        EventId = 202, 
        Level = LogLevel.Critical, 
        Message = "Migration failed for {DbContextName}. Halting application.")]
    private partial void LogMigrationCriticalFailure(Exception ex, string dbContextName);
}