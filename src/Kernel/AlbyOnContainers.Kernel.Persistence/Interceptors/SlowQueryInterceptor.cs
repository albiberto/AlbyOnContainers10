namespace AlbyOnContainers.Kernel.Persistence.Interceptors;

using System.Data.Common;
using System.Diagnostics.Metrics;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Options;

/// <summary>
/// Per-<typeparamref name="TDbContext"/> interceptor that logs slow SQL commands and emits
/// a counter metric. Scoped on the specific DbContext type so that multiple DbContexts in
/// the same process can have independent metric prefixes (and observability cardinality).
/// </summary>
internal sealed partial class SlowQueryInterceptor<TDbContext> : DbCommandInterceptor
    where TDbContext : DbContext
{
    private readonly ILogger<SlowQueryInterceptor<TDbContext>> _logger;
    private readonly string _logPrefix;
    private readonly PersistenceOptions _options;
    private readonly Counter<long> _slowQueryCounter;

    public SlowQueryInterceptor(
        IMeterFactory meterFactory,
        IOptions<PersistenceOptions> options,
        ILogger<SlowQueryInterceptor<TDbContext>> logger)
    {
        _logger = logger;
        _options = options.Value;

        // Each DbContext gets its own prefix: explicit override > options.MetricPrefix > TDbContext.Name.
        var prefix = string.IsNullOrWhiteSpace(_options.MetricPrefix)
            ? typeof(TDbContext).Name.ToLowerInvariant()
            : _options.MetricPrefix.ToLowerInvariant();

        _logPrefix = $"[{prefix.ToUpperInvariant()} PERSISTENCE]";

        var meter = meterFactory.Create(new("AlbyOnContainers.Kernel.Persistence"));
        var metricName = $"{prefix}.persistence.slow_queries.total";

        _slowQueryCounter = meter.CreateCounter<long>(
            metricName,
            string.Empty,
            "Total number of database queries exceeding the slow query threshold.");
    }

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

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    private void LogAndMeasureIfSlow(DbCommand command, CommandExecutedEventData eventData)
    {
        if (eventData.Duration.TotalMilliseconds <= _options.SlowQueryThresholdMs) return;

        var rawSql = command.CommandText;
        var cleanSql = WhitespaceRegex().Replace(rawSql, " ").Trim();

        if (cleanSql.Length > 500) cleanSql = $"{cleanSql[..500]}... [TRUNCATED]";

        _logger.LogWarning("{Prefix} Slow Query Detected ({Duration}ms): {Sql}",
            _logPrefix, (int)eventData.Duration.TotalMilliseconds, cleanSql);

        var operationType = command.CommandText
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault()?.ToUpperInvariant() ?? "UNKNOWN";

        var dbName = eventData.Context?.Database.GetDbConnection().Database ?? "unknown";

        _slowQueryCounter.Add(1, new("db.operation", operationType), new("db.database", dbName));
    }
}