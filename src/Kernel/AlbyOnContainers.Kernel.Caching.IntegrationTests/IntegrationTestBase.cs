namespace AlbyOnContainers.Kernel.Caching.IntegrationTests;

using AlbyOnContainers.Kernel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;

public abstract class IntegrationTestBase
{
    private sealed class TestKernelBuilder(IHostApplicationBuilder hostBuilder) : IKernelBuilder
    {
        public IHostApplicationBuilder Host { get; } = hostBuilder;
    }

    /// <summary>
    ///     Builds an isolated host wired with the Caching kernel module connected to the shared Redis testcontainer.
    ///     Each call creates a fresh <see cref="IConnectionMultiplexer" />, simulating a separate process attached
    ///     to the same Redis instance — which is exactly what's needed to exercise the FusionCache backplane.
    /// </summary>
    protected static async Task<IHost> CreateHostAsync()
    {
        var connectionString = SharedTestContext.RedisContainer.GetConnectionString();
        var multiplexer = await ConnectionMultiplexer.ConnectAsync(connectionString);

        var hostBuilder = Host.CreateApplicationBuilder();
        hostBuilder.Services.AddSingleton<IConnectionMultiplexer>(multiplexer);

        var kernelBuilder = new TestKernelBuilder(hostBuilder);
        kernelBuilder.WithCaching();

        var host = hostBuilder.Build();
        await host.StartAsync();
        return host;
    }
}
