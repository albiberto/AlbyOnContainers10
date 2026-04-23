using Microsoft.Extensions.DependencyInjection;

namespace AlbyOnContainers.Kernel.Modules;

public static class DistributedLocksExtensions
{
    /// <summary>
    /// Configures distributed locks using Redis. Essential for synchronizing Blazor interactions
    /// and distributed processing across multiple instances.
    /// </summary>
    /// <param name="builder">The kernel builder.</param>
    /// <returns>The kernel builder for chaining.</returns>
    public static IAlbyKernelBuilder WithDistributedLocks(this IAlbyKernelBuilder builder)
    {
        // Centralized logic for distributed locks, wraps the existing shared mechanism
        builder.Services.AddDistributedLocks(builder.Configuration);

        return builder;
    }
}
