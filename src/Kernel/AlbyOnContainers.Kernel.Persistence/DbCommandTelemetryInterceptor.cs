using System.Data.Common;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AlbyOnContainers.Kernel.Persistence.Options;

namespace AlbyOnContainers.Kernel.Persistence;

/// <summary>
/// A Singleton EF Core interceptor responsible solely for slow query and failed query logging.
/// Metrics (duration histograms, error counters) are delegated to the OpenTelemetry
/// EF Core instrumentation layer (OpenTelemetry.Instrumentation.EntityFrameworkCore).
/// </summary>
public sealed partial class DbCommandTelemetryInterceptor(
    ILogger<DbCommandTelemetryInterceptor> logger,
    IOptions<PersistenceOptions> options) : DbCommandInterceptor
{
    // ─── Executed (success) ─────────────────────────────────────────────────

    public override DbDataReader ReaderExecuted(
        DbCommand command, CommandExecutedEventData eventData, DbDataReader result)
    {
        CheckSlowQuery(command, eventData);
        return result;
    }

    public override ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result,
        CancellationToken cancellationToken = default)
    {
        CheckSlowQuery(command, eventData);
        return ValueTask.FromResult(result);
    }

    public override object? ScalarExecuted(
        DbCommand command, CommandExecutedEventData eventData, object? result)
    {
        CheckSlowQuery(command, eventData);
        return result;
    }

    public override ValueTask<object?> ScalarExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        object? result,
        CancellationToken cancellationToken = default)
    {
        CheckSlowQuery(command, eventData);
        return ValueTask.FromResult(result);
    }

    public override int NonQueryExecuted(
        DbCommand command, CommandExecutedEventData eventData, int result)
    {
        CheckSlowQuery(command, eventData);
        return result;
    }

    public override ValueTask<int> NonQueryExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        CheckSlowQuery(command, eventData);
        return ValueTask.FromResult(result);
    }

    // ─── Failed ─────────────────────────────────────────────────────────────

    public override void CommandFailed(DbCommand command, CommandErrorEventData eventData)
        => LogFailedCommand(command, eventData);

    public override Task CommandFailedAsync(
        DbCommand command,
        CommandErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        LogFailedCommand(command, eventData);
        return Task.CompletedTask;
    }

    // ─── Core logic ─────────────────────────────────────────────────────────

    private void CheckSlowQuery(DbCommand command, CommandExecutedEventData eventData)
    {
        var durationMs = eventData.Duration.TotalMilliseconds;
        var threshold = options.Value.SlowCommandThresholdMs;

        if (durationMs < threshold)
        {
            return;
        }

        var operation = GetOperation(command);
        var database = eventData.Context?.Database.GetDbConnection().Database ?? "unknown";

        logger.LogWarning(
            "Slow EF Core command detected. Operation: {Operation}, Duration: {DurationMs} ms, " +
            "Threshold: {ThresholdMs} ms, Database: {Database}, CommandText: {CommandText}",
            operation,
            durationMs,
            threshold,
            database,
            NormalizeCommandText(command.CommandText));
    }

    private void LogFailedCommand(DbCommand command, CommandErrorEventData eventData)
    {
        var operation = GetOperation(command);
        var database = eventData.Context?.Database.GetDbConnection().Database ?? "unknown";

        logger.LogError(
            eventData.Exception,
            "EF Core command failed. Operation: {Operation}, Duration: {DurationMs} ms, " +
            "Database: {Database}, CommandText: {CommandText}",
            operation,
            eventData.Duration.TotalMilliseconds,
            database,
            NormalizeCommandText(command.CommandText));
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static string GetOperation(DbCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.CommandText))
        {
            return command.CommandType.ToString();
        }

        var firstToken = command.CommandText
            .Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();

        return string.IsNullOrWhiteSpace(firstToken)
            ? command.CommandType.ToString()
            : firstToken.ToUpperInvariant();
    }

    private static string NormalizeCommandText(string commandText)
    {
        if (string.IsNullOrWhiteSpace(commandText))
        {
            return string.Empty;
        }

        var normalized = WhitespaceRegex().Replace(commandText, " ").Trim();
        return normalized.Length <= 400 ? normalized : $"{normalized[..400]}...";
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
