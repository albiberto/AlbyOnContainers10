using Microsoft.Extensions.Hosting;

namespace AlbyOnContainers.Kernel;

internal sealed class KernelBuilder(IHostApplicationBuilder host) : IKernelBuilder
{
    public IHostApplicationBuilder Host { get; } = host;
}