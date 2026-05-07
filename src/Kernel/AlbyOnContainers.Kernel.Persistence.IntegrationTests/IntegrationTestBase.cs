using AlbyOnContainers.Kernel;
using AlbyOnContainers.Kernel.Persistence.IntegrationTests.Fakes;
using AlbyOnContainers.Kernel.Security.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using NUnit.Framework;
using Respawn;
using Respawn.Graph;

namespace AlbyOnContainers.Kernel.Persistence.IntegrationTests;

public abstract class IntegrationTestBase
{
    private Respawner _respawner = null!;
    private ServiceProvider _rootServiceProvider = null!;
    protected IServiceScope Scope { get; private set; } = null!;
    protected TestDbContext DbContext { get; private set; } = null!;
    protected StubCurrentUserService CurrentUserService => Scope.ServiceProvider.GetRequiredService<StubCurrentUserService>();

    private sealed class TestKernelBuilder(IHostApplicationBuilder hostBuilder) : IKernelBuilder
    {
        public IHostApplicationBuilder Host { get; } = hostBuilder;
    }

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        var connectionString = SharedTestContext.PostgresContainer.GetConnectionString();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:TestDb"] = connectionString,
                ["Persistence:RunMigrationsOnStartup"] = "false",
                ["Persistence:MetricPrefix"] = "integration_tests"
            })
            .Build();

        var hostBuilder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder();
        hostBuilder.Configuration.AddConfiguration(configuration);

        hostBuilder.Services.AddScoped<StubCurrentUserService>();
        hostBuilder.Services.AddScoped<ICurrentUserService>(sp => sp.GetRequiredService<StubCurrentUserService>());

        hostBuilder.Services.AddSingleton(TimeProvider.System);

        var kernelBuilder = new TestKernelBuilder(hostBuilder);

        // Template Method pattern hook: subclasses can register
        // additional services (e.g., IModelConfigurationPlugin) before
        // WithPersistence captures the container with sp.GetServices<T>().
        RegisterAdditionalServices(hostBuilder.Services);

        kernelBuilder.WithPersistence<TestDbContext>((sp, options) =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            options.UseNpgsql(config.GetConnectionString("TestDb"));
        });

        _rootServiceProvider = hostBuilder.Services.BuildServiceProvider();

        using var initScope = _rootServiceProvider.CreateScope();
        var initDbContext = initScope.ServiceProvider.GetRequiredService<TestDbContext>();
        
        await initDbContext.Database.EnsureCreatedAsync();

        var dbConnection = initDbContext.Database.GetDbConnection();
        await dbConnection.OpenAsync();

        _respawner = await Respawner.CreateAsync(dbConnection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = ["public"]
        });
    }

    [SetUp]
    public async Task SetUp()
    {
        using var tempScope = _rootServiceProvider.CreateScope();
        var tempDbContext = tempScope.ServiceProvider.GetRequiredService<TestDbContext>();
        var dbConnection = tempDbContext.Database.GetDbConnection();
        await dbConnection.OpenAsync();
        
        await _respawner.ResetAsync(dbConnection);

        Scope = _rootServiceProvider.CreateScope();
        DbContext = Scope.ServiceProvider.GetRequiredService<TestDbContext>();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        // Disposes the root ServiceProvider at the end of the fixture.
        // Required by the NUnit1032 analyzer for fixture-level disposables.
        _rootServiceProvider.Dispose();
    }

    [TearDown]
    public void TearDown()
    {
        // Explicit disposal of DbContext required by the NUnit1032 analyzer.
        // The Scope is disposed second: it also disposes other scoped resources.
        DbContext?.Dispose();
        DbContext = null!;
        Scope?.Dispose();
    }

    /// <summary>
    /// Template Method pattern hook. Subclasses can override
    /// this method to register additional services (e.g., EF Core plugins)
    /// in the DI container <strong>before</strong> <c>WithPersistence</c> is
    /// invoked and the plugins are captured in the <c>KernelModelPluginsExtension</c>.
    /// </summary>
    /// <param name="services">The service collection of the host being built.</param>
    protected virtual void RegisterAdditionalServices(IServiceCollection services) { }
}
