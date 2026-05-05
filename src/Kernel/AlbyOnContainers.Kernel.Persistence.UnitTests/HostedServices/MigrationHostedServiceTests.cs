namespace AlbyOnContainers.Kernel.Persistence.UnitTests.HostedServices;

using AlbyOnContainers.Kernel.Persistence.HostedServices;
using AlbyOnContainers.Kernel.Persistence.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

public sealed class MigrationHostedServiceTests
{
    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task StartAsync_does_not_create_scope_when_migrations_are_disabled(string environmentName)
    {
        var scopeFactory = new ThrowingScopeFactory();
        var lifetime = new RecordingHostApplicationLifetime();
        var service = new MigrationHostedService<TestDbContext>(
            scopeFactory,
            Options.Create(new PersistenceOptions { RunMigrationsOnStartup = false }),
            new StubHostEnvironment(environmentName),
            lifetime,
            NullLogger<MigrationHostedService<TestDbContext>>.Instance);

        await service.StartAsync(CancellationToken.None);

        Assert.Equal(0, scopeFactory.CreateScopeCallCount);
        Assert.Equal(0, lifetime.StopApplicationCallCount);
    }

    [Fact]
    public async Task StopAsync_completes_without_work()
    {
        var service = new MigrationHostedService<TestDbContext>(
            new ThrowingScopeFactory(),
            Options.Create(new PersistenceOptions()),
            new StubHostEnvironment(Environments.Development),
            new RecordingHostApplicationLifetime(),
            NullLogger<MigrationHostedService<TestDbContext>>.Instance);

        await service.StopAsync(CancellationToken.None);
    }

    private sealed class TestDbContext : DbContext;

    private sealed class ThrowingScopeFactory : IServiceScopeFactory
    {
        public int CreateScopeCallCount { get; private set; }

        public IServiceScope CreateScope()
        {
            CreateScopeCallCount++;
            throw new InvalidOperationException("A scope should not be created for this test case.");
        }
    }

    private sealed class StubHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "Persistence.UnitTests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class RecordingHostApplicationLifetime : IHostApplicationLifetime
    {
        public int StopApplicationCallCount { get; private set; }
        public CancellationToken ApplicationStarted => CancellationToken.None;
        public CancellationToken ApplicationStopping => CancellationToken.None;
        public CancellationToken ApplicationStopped => CancellationToken.None;

        public void StopApplication() => StopApplicationCallCount++;
    }
}
