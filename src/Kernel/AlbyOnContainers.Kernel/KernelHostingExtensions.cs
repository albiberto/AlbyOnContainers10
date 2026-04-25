using AlbyOnContainers.Kernel.Abstraction;
using Microsoft.Extensions.Hosting;

namespace AlbyOnContainers.Kernel;

public static class KernelHostingExtensions
{
    public static IKernelBuilder AddKernel(this IHostApplicationBuilder builder) => new KernelBuilder(builder);
}