using Microsoft.Extensions.Hosting;

namespace AlbyOnContainers.Kernel;

public interface IAlbyKernelBuilder
{
    IHostApplicationBuilder Host { get; }
}

internal sealed class AlbyKernelBuilder(IHostApplicationBuilder host) : IAlbyKernelBuilder
{
    public IHostApplicationBuilder Host { get; } = host;
}

public static class KernelHostingExtensions
{
    public static IAlbyKernelBuilder AddAlbyKernel(this IHostApplicationBuilder builder)
    {
        return new AlbyKernelBuilder(builder);
    }
}