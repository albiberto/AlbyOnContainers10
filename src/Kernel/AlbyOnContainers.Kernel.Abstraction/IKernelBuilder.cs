using Microsoft.Extensions.Hosting;

namespace AlbyOnContainers.Kernel.Abstraction;

public interface IKernelBuilder
{
    IHostApplicationBuilder Host { get; }
}