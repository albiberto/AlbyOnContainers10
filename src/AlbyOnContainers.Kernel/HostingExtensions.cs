using Microsoft.Extensions.Hosting;

namespace Alby.Kernel.Hosting;

/// <summary>
/// Entry point for the Alby Kernel SDK configuration.
/// </summary>
public static class HostingExtensions
{
    /// <summary>
    /// Initializes the Alby Kernel Builder to fluently configure enterprise infrastructure
    /// (Security, Messaging, CQRS, Caching, Persistence, etc.).
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    /// <returns>A fluent builder for Alby Kernel.</returns>
    public static IAlbyKernelBuilder AddAlbyKernel(this IHostApplicationBuilder builder)
    {
        return new AlbyKernelBu