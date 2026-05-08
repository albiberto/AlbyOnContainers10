namespace AlbyOnContainers.Kernel.Persistence.HostedServices;

using System.Diagnostics;
using Microsoft.Extensions.Options;
using Options;

/// <summary>
///     Shared <see cref="ActivitySource" /> for migration hosted services. The source name
///     is namespaced via <see cref="PersistenceOptions.MetricPrefix" /> so each microservice
///     emits activities under its own OpenTelemetry namespace.
/// </summary>
public sealed class MigrationTelemetry(IOptions<PersistenceOptions> options) : IDisposable
{
    public ActivitySource ActivitySource { get; } = new($"{options.Value.MetricPrefix}.persistence.migration");

    public void Dispose() => ActivitySource.Dispose();
}
