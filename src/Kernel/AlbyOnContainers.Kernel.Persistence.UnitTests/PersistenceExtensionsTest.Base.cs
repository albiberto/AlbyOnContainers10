namespace AlbyOnContainers.Kernel.Persistence.UnitTests;

using AlbyOnContainers.Kernel.Security.Abstractions;
using Medallion.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using NUnit.Framework;

/// <summary>
///     Base class for persistence tests.
///     Manages the strict lifecycle of the HostBuilder and provides shared assertion logic.
/// </summary>
public abstract class PersistenceTestBase
{
    private IHost? _host;

    protected HostApplicationBuilder HostBuilder = null!;
    protected IKernelBuilder KernelBuilder = null!;

    [SetUp]
    public void SetUpBase()
    {
        // Arrange: Initialize a fresh builder and kernel for each test to guarantee complete isolation.
        HostBuilder = Host.CreateApplicationBuilder();

        HostBuilder.Services.AddSingleton(Substitute.For<ICurrentUserService>());
        HostBuilder.Services.AddSingleton(Substitute.For<IDistributedLockProvider>());
        
        KernelBuilder = HostBuilder.AddKernel();
    }

    [TearDown]
    public void TearDownBase() => _host?.Dispose();

    protected IHost BuildHost()
    {
        _host = HostBuilder.Build();
        return _host;
    }
}
