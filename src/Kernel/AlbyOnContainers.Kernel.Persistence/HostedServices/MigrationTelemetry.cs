namespace AlbyOnContainers.Kernel.Persistence.HostedServices;

using System.Diagnostics;

/// <summary>
/// Shared <see cref="ActivitySource"/> for all migration hosted services so that
/// every closed generic <see cref="MigrationHostedService{TDbContext}"/> emits
/// activities under a single, stable telemetry name.
/// </summary>
internal static class MigrationTelemetry
{
    public static readonly ActivitySource ActivitySource = new("AlbyOnContainers.Kernel.Persistence.Migrations");
}