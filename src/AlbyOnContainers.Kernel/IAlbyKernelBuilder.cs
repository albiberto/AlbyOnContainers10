using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Alby.Kernel.Hosting;

/// <summary>
/// Builder interface for configuring the Alby Kernel components.
/// This pattern allows developers to fluently chain configuration methods
/// and hides the complexity of the underlying infrastructure setup.
/// </summary>
public interface IAlbyKernelBuilder
{
    /// <summary>
    /// Gets the underlying host application builder.
    /// </summary>
    IHostApplicationBuilder HostBuilder { get; }

    /// <summary>
    /// Gets the service collection.
    /// </summary>
    IServiceCollection Services { get; }

    /// <summary>
    /// Gets the configuration.
    /// </summary>
    IConfiguration Configuration { get; }
}
