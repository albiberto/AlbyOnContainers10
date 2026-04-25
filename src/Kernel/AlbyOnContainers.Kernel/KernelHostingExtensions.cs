using AlbyOnContainers.Kernel.Abstraction;
using Microsoft.Extensions.Hosting;

namespace AlbyOnContainers.Kernel;

public static class KernelHostingExtensions
{
    public static IKernelBuilder AddAlbyKernel(this IHostApplicationBuilder builder)
    {
        return new KernelBuilder(builder);
    }
}