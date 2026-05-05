namespace AlbyOnContainers.Kernel.Resilience.UnitTests;

using Microsoft.Extensions.Hosting;
using NUnit.Framework;

/// <summary>
///     Base class for resilience tests.
///     Manages the strict lifecycle of the HostBuilder and provides shared assertion logic.
/// </summary>
public abstract class ResilienceTestBase
{
    private IHost? _host;
    
    protected HostApplicationBuilder HostBuilder = null!;
    protected IKernelBuilder KernelBuilder = null!;

    [SetUp]
    public void SetUpBase()
    {
        // Arrange: Initialize a fresh builder and kernel for each test to guarantee complete isolation.
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
}