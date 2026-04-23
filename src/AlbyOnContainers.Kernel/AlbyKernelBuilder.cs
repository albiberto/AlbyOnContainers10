using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AlbyOnContainers.Kernel;

/// <summary>
/// Internal implementation of the IAlbyKernelBuilder.
/// </summary>
internal sealed class AlbyKernelBuilder(IHostApplicationBuilder hostBuilder) : IAlbyKernelBuilder
{
    public IHostApplicationBuilder HostBuilder { get; } = hostBuilder;
    
    public IServiceCollection Services => HostBuilder.Services;
    
    public IConfiguration Configuration => HostBuilder.Configuration;
}
