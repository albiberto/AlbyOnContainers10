using Microsoft.Extensions.Hosting;

namespace AlbyOnContainers.Kernel;

public interface IKernelBuilder
{
    IHostApplicationBuilder Host { get; }
}