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
                ["Persistence:RunMigrationsOnStartup"] = "false"
            })
            .Build();

        var hostBuilder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder();
        hostBuilder.Configuration.AddConfiguration(configuration);

        var currentUserServiceMock = Substitute.For<ICurrentUserService>();
        currentUserServiceMock.UserId.Returns("TestUser");
        hostBuilder.Services.AddSingleton(currentUserServiceMock);

        hostBuilder.Services.AddSingleton(TimeProvider.System);

        var kernelBuilder = new TestKernelBuilder(hostBuilder);

        // Hook del Template Method pattern: le sottoclassi possono registrare
        // servizi aggiuntivi (es. IModelConfigurationPlugin) prima che
        // WithPersistence catturi il container con sp.GetServices<T>().
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
        // Smaltisce il ServiceProvider radice al termine della fixture.
        // Richiesto dall'analizzatore NUnit1032 per i disposable a livello di fixture.
        _rootServiceProvider?.Dispose();
    }

    [TearDown]
    public void TearDown()
    {
        // Dispose esplicito di DbContext richiesto dall'analizzatore NUnit1032.
        // Lo Scope viene disposto per secondo: smaltisce anche le altre risorse scoped.
        DbContext?.Dispose();
        DbContext = null!;
        Scope?.Dispose();
    }

    /// <summary>
    /// Hook del Template Method pattern. Le sottoclassi possono sovrascrivere
    /// questo metodo per registrare servizi aggiuntivi (es. plugin EF Core)
    /// nel container DI <strong>prima</strong> che <c>WithPersistence</c> venga
    /// invocato e i plugin vengano catturati nella <c>KernelModelPluginsExtension</c>.
    /// </summary>
    /// <param name="services">La collezione di servizi dell'host in costruzione.</param>
    protected virtual void RegisterAdditionalServices(IServiceCollection services) { }
}
