namespace AlbyOnContainers.Kernel.Persistence.HostedServices;

using Microsoft.Extensions.Logging;

public sealed partial class MigrationHostedService<TDbContext>
{
    [LoggerMessage(EventId = 200, Level = LogLevel.Information, Message = "Applying {Count} pending migrations for {DbContextName}...")]
    private partial void LogApplyingMigrations(int count, string dbContextName);

    [LoggerMessage(EventId = 201, Level = LogLevel.Information, Message = "Applied {Count} migrations for {DbContextName}.")]
    private partial void LogMigrationsApplied(int count, string dbContextName);

    [LoggerMessage(EventId = 202, Level = LogLevel.Critical, Message = "Migration failed for {DbContextName}. Halting application.")]
    private partial void LogMigrationCriticalFailure(Exception ex, string dbContextName);

    [LoggerMessage(EventId = 203, Level = LogLevel.Information, Message = "Migrations disabled by configuration for {DbContextName}.")]
    private partial void LogMigrationsDisabled(string dbContextName);

    [LoggerMessage(EventId = 204, Level = LogLevel.Debug, Message = "No pending migrations for {DbContextName}.")]
    private partial void LogNoPendingMigrations(string dbContextName);

    [LoggerMessage(EventId = 208, Level = LogLevel.Warning, Message = "Migration for {DbContextName} was cancelled by the host shutdown signal.")]
    private partial void LogMigrationCancelled(string dbContextName);
}