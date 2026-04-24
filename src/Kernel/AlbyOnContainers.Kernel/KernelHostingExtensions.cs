using AlbyOnContainers.Kernel.Abstraction;
using AlbyOnContainers.Kernel.Security;
using AlbyOnContainers.Kernel.Security.Options;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AlbyOnContainers.Kernel;

public static class KernelHostingExtensions
{
    public static IKernelBuilder AddAlbyKernel(this IHostApplicationBuilder builder)
    {
        return new KernelBuilder(builder);
    }
}