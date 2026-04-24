using System.Data.Common;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace AlbyOnContainers.Kernel.Persistence;

public sealed partial class DbCommandTelemetryInterceptor(
    ILogger<DbCommandTelemetryInterceptor> logger,
    IConfiguration configuration) : DbCommandInterceptor
{
    private static readonly Meter Meter = new("AlbyOnContainers.Kernel.Persistence");
    private static readonly Histogram<double> CommandDuration = Meter.CreateHistogram<double>(
        "pim_efcore_command_duration",
        unit: "ms",
        description: "Execution time for EF Core database commands.");
    private static readonly Counter<long> CommandErrors = Meter.CreateCounter<long>(
        "pim_efcore_command_errors",
        description: "Number of failed EF Core database commands.");

    public override DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData eventData, DbDataReader result)
    {
        RecordCommand(command, eventData);
        return result;
    }

    public override ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result,
        CancellationToken cancellationToken = default)
    {
        RecordCommand(command, eventData);
        return ValueTask.FromResult(result);
    }

    public override object? ScalarExecuted(DbCommand command, CommandExecutedEventData eventData, object? result)
    {
        RecordCommand(command, eventData);
        return result;
    }

    public override ValueTask<object?> ScalarExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        object? result,
        CancellationToken cancellationToken = default)
    {
        RecordCommand(command, eventData);
        return ValueTask.FromResult(result);
    }

    public override int NonQueryExecuted(DbCommand command, CommandExecutedEventData eventData, int result)
    {
        RecordCommand(command, eventData);
        return result;
    }

    public override ValueTask<int> NonQueryExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        RecordCommand(command, eventData);
        return ValueTask.FromResult(result);
    }

    public override void CommandFailed(DbCommand command, CommandErrorEventData eventData)
    {
        RecordFailure(command, eventData);
    }

    public override Task CommandFailedAsync(
        DbCommand command,
        CommandErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        RecordFailure(command, eventData);
        return Task.CompletedTask;
    }

    private void RecordCommand(DbCommand command, CommandExecutedEventData eventData)
    {
        var durationMs = eventData.Duration.TotalMilliseconds;
        var operation = GetOperation(command);
        var databaseName = eventData.Context?.Database.GetDbConnection().Database ?? "unknown";

        var tags = new TagList
        {
            { "db.system", "postgresql" },
            { "db.name", databaseName },
            { "db.operation", operation }
        };

        CommandDuration.Record(durationMs, tags);

        var slowCommandThresholdMs = configuration.GetValue<int>("EfCore:SlowCommandThresholdMs", 500);
        if (durationMs < slowCommandThresholdMs)
        {
            return;
        }

        logger.LogWarning(
            "PIM EFCore slow command detected. Operation: {Operation}. DurationMs: {DurationMs}. Database: {Database}. CommandText: {CommandText}",
            operation,
            durationMs,
            databaseName,
            NormalizeCommandText(command.CommandText));
    }

    private void RecordFailure(DbCommand command, CommandErrorEventData eventData)
    {
        var operation = GetOperation(command);
        var databaseName = eventData.Context?.Database.GetDbConnection().Database ?? "unknown";

        var tags = new TagList
        {
            { "db.system", "postgresql" },
            { "db.name", databaseName },
            { "db.operation", operation }
        };

        CommandErrors.Add(1, tags);

        logger.LogError(
            eventData.Exception,
            "PIM EFCore command failed. Operation: {Operation}. DurationMs: {DurationMs}. Database: {Database}. CommandText: {CommandText}",
            operation,
            eventData.Duration.TotalMilliseconds,
            databaseName,
            NormalizeCommandText(command.CommandText));
    }

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
