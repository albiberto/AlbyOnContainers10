using Microsoft.Extensions.Hosting;

namespace AlbyOnContainers.Kernel;

/// <summary>
/// Bootstraps the kernel fluent builder on top of a host application builder.
/// </summary>
public static class KernelHostingExtensions
{
    /// <summary>
    /// Returns a new <see cref="IKernelBuilder"/> attached to <paramref name="builder"/>.
    /// Subsequent <c>.WithXxx(...)</c> calls register kernel modules into the same host.
    /// </summary>
    public static IKernelBuilder AddKernel(this IHostApplicationBuilder builder) => new KernelBuilder(builder);
}