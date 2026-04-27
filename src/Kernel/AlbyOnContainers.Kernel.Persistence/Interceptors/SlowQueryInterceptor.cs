namespace AlbyOnContainers.Kernel.Persistence.Interceptors;

using System;
using System.Data.Common;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Options;

// AGGIUNTA LA KEYWORD partial QUI SOTTO
internal sealed partial class SlowQueryInterceptor : DbCommandInterceptor
{
    private readonly ILogger<SlowQueryInterceptor> _logger;
    private readonly string _logPrefix;
    private readonly PersistenceOptions _options;
    private readonly Counter<long> _slowQueryCounter;

    public SlowQueryInterceptor(IMeterFactory meterFactory, IOptions<PersistenceOptions> options, ILogger<SlowQueryInterceptor> logger)
    {
        _logger = logger;
        _options = options.Value;
        _logPrefix = $"[{_options.MetricPrefix.ToUpperInvariant()} PERSISTENCE]";

        var meter = meterFactory.Create(new("AlbyOnContainers.Kernel.Persistence"));

        var metricName = $"{_options.MetricPrefix.ToLowerInvariant()}.persistence.slow_queries.total";

        _slowQueryCounter = meter.CreateCounter<long>(
            metricName,
            "{query}",
            "Total number of database queries exceeding the slow query threshold.");
    }

    // --- 1. Reader Executed ---

    public override DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData eventData, DbDataReader result)
    {
        LogAndMeasureIfSlow(command, eventData);
        return base.ReaderExecuted(command, eventData, result);
    }

    public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
    {
        LogAndMeasureIfSlow(command, eventData);
        return base.ReaderExecutedAsync(command, eventData, result, cancellationToken);
    }

    // --- 2. Scalar Executed ---

    public override object? ScalarExecuted(DbCommand command, CommandExecutedEventData eventData, object? result)
    {
        LogAndMeasureIfSlow(command, eventData);
        return base.ScalarExecuted(command, eventData, result);
    }

    public override ValueTask<object?> ScalarExecutedAsync(DbCommand command, CommandExecutedEventData eventData, object? result, CancellationToken cancellationToken = default)
    {
        LogAndMeasureIfSlow(command, eventData);
        return base.ScalarExecutedAsync(command, eventData, result, cancellationToken);
    }

    // --- 3. NonQuery Executed ---

    public override int NonQueryExecuted(DbCommand command, CommandExecutedEventData eventData, int result)
    {
        LogAndMeasureIfSlow(command, eventData);
        return base.NonQueryExecuted(command, eventData, result);
    }

    public override ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        LogAndMeasureIfSlow(command, eventData);
        return base.NonQueryExecutedAsync(command, eventData, result, cancellationToken);
    }

    // --- Shared Telemetry Logic ---

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    private void LogAndMeasureIfSlow(DbCommand command, CommandExecutedEventData eventData)
    {
        if (!(eventData.Duration.TotalMilliseconds > _options.SlowQueryThresholdMs)) return;

        var rawSql = command.CommandText;
        var cleanSql = WhitespaceRegex().Replace(rawSql, " ").Trim();

        if (cleanSql.Length > 500) cleanSql = $"{cleanSql[..500]}... [TRUNCATED]";

        _logger.LogWarning("{Prefix} Slow Query Detected ({Duration}ms): {Sql}", _logPrefix, (int)eventData.Duration.TotalMilliseconds, cleanSql);

        var operationType = command.CommandText
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault()?.ToUpperInvariant() ?? "UNKNOWN";

        // Estrazione sicura del nome del database
        var dbName = eventData.Context?.Database.GetDbConnection().Database ?? "unknown";

        _slowQueryCounter.Add(1, new("db.operation", operationType), new("db.database", dbName));
    }
}