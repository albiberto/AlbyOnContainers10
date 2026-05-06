namespace AlbyOnContainers.Kernel.Persistence.UnitTests;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

public abstract class PersistenceTestBase
{
    private IHost? _host;

    protected HostApplicationBuilder HostBuilder = null!;
    protected IKernelBuilder KernelBuilder = null!;

    [SetUp]
    public void SetUpBase()
    {
        // Isolamento totale: un nuovo Host e un nuovo Kernel per ogni test
        HostBuilder = Host.CreateApplicationBuilder();
        KernelBuilder = HostBuilder.AddKernel();
    }

    [TearDown]
    public void TearDownBase() => _host?.Dispose();

    protected IHost BuildHost()
    {
        _host = HostBuilder.Build();
        return _host;
    }

    protected void AddInMemoryConfiguration(Dictionary<string, string?> appSettings)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(appSettings)
            .Build();

        HostBuilder.Configuration.AddConfiguration(configuration);
    }
}