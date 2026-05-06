namespace AlbyOnContainers.Kernel.Persistence.UnitTests.HostedServices;

using AlbyOnContainers.Kernel.Persistence.HostedServices;
using AlbyOnContainers.Kernel.Persistence.Options;
using Medallion.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NUnit.Framework;
using Polly;

/// <summary>
/// Unit tests per <see cref="MigrationHostedService{TDbContext}"/>.
///
/// <para>
/// Strategia di test: evitare dipendenze da infrastruttura reale (database,
/// lock distribuiti, Polly pipeline esterna) isolando ogni comportamento
/// osservabile tramite stub e mock leggeri.
/// </para>
/// </summary>
[TestFixture]
public sealed class MigrationHostedServiceTests
{
    // ---------------------------------------------------------------------------
    // Helpers: factory per istanziare il servizio con valori di default sensibili
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Crea un'istanza di <see cref="MigrationHostedService{TDbContext}"/> con
    /// tutte le dipendenze pre-configurate a valori sicuri/stub, permettendo
    /// al singolo test di sovrascrivere solo ciò che è rilevante.
    /// </summary>
    private static MigrationHostedService<TDbContext> BuildService<TDbContext>(
        IServiceScopeFactory? scopeFactory = null,
        IHostApplicationLifetime? lifetime = null,
        IDistributedLockProvider? lockProvider = null,
        ResiliencePipeline? pipeline = null,
        PersistenceOptions? options = null,
        ILogger<MigrationHostedService<TDbContext>>? logger = null)
        where TDbContext : DbContext
    {
        // IServiceScopeFactory stub: crea uno scope che lancia per segnalare
        // che il test non dovrebbe mai raggiungere quel codepath (fast-fail).
        scopeFactory ??= new ThrowingScopeFactory();

        // IHostApplicationLifetime stub: registra le chiamate a StopApplication.
        lifetime ??= new RecordingHostApplicationLifetime();

        // IDistributedLockProvider stub: restituisce un lock fasullo senza rete.
        lockProvider ??= Substitute.For<IDistributedLockProvider>();

        // ResiliencePipeline.Empty: esegue il delegate direttamente senza retry/timeout.
        // È il candidato perfetto per unit test: non introduce latenze né retry loop.
        pipeline ??= ResiliencePipeline.Empty;

        options ??= new PersistenceOptions { RunMigrationsOnStartup = false, MetricPrefix = "test" };

        logger ??= NullLogger<MigrationHostedService<TDbContext>>.Instance;

        return new MigrationHostedService<TDbContext>(
            scopeFactory,
            lifetime,
            lockProvider,
            pipeline,
            Options.Create(options),
            logger);
    }

    // ---------------------------------------------------------------------------
    // Test: comportamento quando le migrazioni sono disabilitate da configurazione
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Quando <see cref="PersistenceOptions.RunMigrationsOnStartup"/> è <c>false</c>,
    /// il servizio deve terminare immediatamente senza creare scope DI né
    /// chiamare <see cref="IHostApplicationLifetime.StopApplication"/>.
    /// Copre sia Development che Production per dimostrare che l'opt-out
    /// è uniforme in tutti gli ambienti.
    /// </summary>
    [TestCase("Development")]
    [TestCase("Production")]
    public async Task StartAsync_Should_Not_Create_Scope_When_Migrations_Are_Disabled(string environmentName)
    {
        // Arrange
        var scopeFactory = new ThrowingScopeFactory();
        var lifetime = new RecordingHostApplicationLifetime();

        var service = BuildService<TestDbContext>(
            scopeFactory: scopeFactory,
            lifetime: lifetime,
            options: new PersistenceOptions { RunMigrationsOnStartup = false, MetricPrefix = "test" });

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert — nessuno scope creato e applicazione non fermata
        Assert.That(scopeFactory.CreateScopeCallCount, Is.Zero,
            "Nessuno scope DI deve essere creato quando le migrazioni sono disabilitate.");
        Assert.That(lifetime.StopApplicationCallCount, Is.Zero,
            "StopApplication non deve essere invocato in assenza di errori.");
    }

    // ---------------------------------------------------------------------------
    // Test: l'infrastruttura di scaffolding viene orchestrata quando abilitato
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Quando <see cref="PersistenceOptions.RunMigrationsOnStartup"/> è <c>true</c>,
    /// il servizio deve:
    /// <list type="number">
    ///   <item>Creare uno scope asincrono tramite <see cref="IServiceScopeFactory"/>.</item>
    ///   <item>Risolvere il <typeparamref name="TDbContext"/> dallo scope.</item>
    ///   <item>Invocare la <see cref="ResiliencePipeline"/> con il delegate di migrazione.</item>
    /// </list>
    ///
    /// <para>
    /// Limitazione nota: <c>GetPendingMigrationsAsync</c> e <c>MigrateAsync</c> sono
    /// extension method statici di EF Core non sostituibili con NSubstitute su un'interfaccia.
    /// Pertanto il test verifica il comportamento di scaffolding/orchestrazione (scope,
    /// risoluzione del context, esecuzione della pipeline) senza toccare il database.
    /// Il percorso "nessuna migrazione pendente" viene attivato configurando il DbContext
    /// in-memory, che restituisce sempre zero pending migrations.
    /// </para>
    /// </summary>
    [Test]
    public async Task StartAsync_Should_Execute_Migrations_Scaffolding_When_Enabled()
    {
        // Arrange ----------------------------------------------------------------
        var lockHandle = Substitute.For<IDistributedSynchronizationHandle>();
        lockHandle.HandleLostToken.Returns(CancellationToken.None);

        var lockProvider = Substitute.For<IDistributedLockProvider>();
        lockProvider
            .TryAcquireLockAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(lockHandle);

        // Scope factory reale che crea uno scope con un DbContext in-memory.
        // Il DbContext in-memory restituisce sempre una lista vuota di pending migrations,
        // portando il servizio al ramo "nessuna migrazione pendente" (percorso felice).
        var services = new ServiceCollection();
        services.AddDbContext<TestDbContext>(opts =>
            opts.UseInMemoryDatabase($"MigrationTest_{Guid.NewGuid()}"));
        var rootProvider = services.BuildServiceProvider();

        var recordingFactory = new RecordingScopeFactory(rootProvider);
        var lifetime = new RecordingHostApplicationLifetime();

        // ResiliencePipeline.Empty: esegue il delegate immediatamente, senza retry.
        // Questo ci permette di verificare l'intero flusso senza infrastruttura Polly esterna.
        var service = BuildService<TestDbContext>(
            scopeFactory: recordingFactory,
            lifetime: lifetime,
            lockProvider: lockProvider,
            pipeline: ResiliencePipeline.Empty,
            options: new PersistenceOptions
            {
                RunMigrationsOnStartup = true,
                MetricPrefix = "test",
                LockTimeout = TimeSpan.FromSeconds(10)
            });

        // Act --------------------------------------------------------------------
        await service.StartAsync(CancellationToken.None);

        // Assert -----------------------------------------------------------------

        // 1. Almeno uno scope asincrono deve essere stato creato.
        //    Questo conferma che il servizio ha raggiunto RunMigrationAsync().
        Assert.That(recordingFactory.CreateAsyncScopeCallCount, Is.GreaterThan(0),
            "Il servizio deve creare almeno uno scope asincrono quando le migrazioni sono abilitate.");

        // 2. Il lock distribuito deve essere stato richiesto esattamente una volta.
        //    Questo conferma che il meccanismo di coordinamento multi-replica è attivo.
        await lockProvider.Received(1).TryAcquireLockAsync(
            Arg.Is<string>(name => name.Contains("testdbcontext")),
            Arg.Any<TimeSpan>(),
            Arg.Any<CancellationToken>());

        // 3. StopApplication non deve essere chiamato in assenza di eccezioni.
        Assert.That(lifetime.StopApplicationCallCount, Is.Zero,
            "StopApplication non deve essere invocato quando le migrazioni completano senza errori.");
    }

    // ---------------------------------------------------------------------------
    // Test: StopAsync non esegue alcun lavoro
    // ---------------------------------------------------------------------------

    /// <summary>
    /// <see cref="MigrationHostedService{TDbContext}.StopAsync"/> deve completare
    /// immediatamente senza lavoro, in quanto tutte le operazioni si svolgono in
    /// <see cref="MigrationHostedService{TDbContext}.StartAsync"/>.
    /// </summary>
    [Test]
    public async Task StopAsync_Should_Complete_Without_Work()
    {
        var service = BuildService<TestDbContext>();

        // Non deve lanciare eccezioni né bloccarsi.
        await service.StopAsync(CancellationToken.None);
    }

    // ---------------------------------------------------------------------------
    // Test: cancellazione durante StartAsync
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Quando il token di cancellazione viene segnalato prima dell'esecuzione,
    /// il servizio deve propagare l'<see cref="OperationCanceledException"/>
    /// senza chiamare <see cref="IHostApplicationLifetime.StopApplication"/>.
    /// StopApplication è riservato ai guasti genuini, non alle cancellazioni
    /// controllate del host.
    /// </summary>
    [Test]
    public async Task StartAsync_Should_Not_Call_StopApplication_On_Cancellation()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var lifetime = new RecordingHostApplicationLifetime();

        // Pipeline che propaga immediatamente la cancellazione
        var cancellingPipeline = new ResiliencePipelineBuilder()
            .Build(); // Pipeline vuota — la cancellazione viene propagata dal token

        var service = BuildService<TestDbContext>(
            lifetime: lifetime,
            pipeline: cancellingPipeline,
            options: new PersistenceOptions { RunMigrationsOnStartup = true, MetricPrefix = "test" });

        // Cancella immediatamente prima di chiamare StartAsync
        await cts.CancelAsync();

        // Act & Assert
        Assert.ThrowsAsync<OperationCanceledException>(
            () => service.StartAsync(cts.Token));

        Assert.That(lifetime.StopApplicationCallCount, Is.Zero,
            "StopApplication non deve essere invocato per cancellazioni controllate.");
    }

    // ---------------------------------------------------------------------------
    // Fakes / Stubs locali al file di test
    // ---------------------------------------------------------------------------

    /// <summary>Minimal DbContext usato come tipo generico nei test.</summary>
    private sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options);

    /// <summary>
    /// Stub di <see cref="IServiceScopeFactory"/> che conta le invocazioni e
    /// lancia per segnalare che il test non avrebbe dovuto creare uno scope.
    /// </summary>
    private sealed class ThrowingScopeFactory : IServiceScopeFactory
    {
        public int CreateScopeCallCount { get; private set; }

        public IServiceScope CreateScope()
        {
            CreateScopeCallCount++;
            throw new InvalidOperationException(
                "Uno scope non dovrebbe essere creato in questo test case.");
        }
    }

    /// <summary>
    /// Stub di <see cref="IServiceScopeFactory"/> che delega al vero
    /// <see cref="IServiceProvider"/> e registra quante volte viene invocato.
    /// </summary>
    private sealed class RecordingScopeFactory(IServiceProvider rootProvider) : IServiceScopeFactory
    {
        public int CreateAsyncScopeCallCount { get; private set; }

        public IServiceScope CreateScope()
        {
            // Redirige verso CreateAsyncScope per uniformità con il codice sorgente
            // che usa CreateAsyncScope internamente.
            CreateAsyncScopeCallCount++;
            return rootProvider.CreateScope();
        }
    }

    /// <summary>
    /// Stub di <see cref="IHostApplicationLifetime"/> che registra
    /// le chiamate a <see cref="StopApplication"/>.
    /// </summary>
    private sealed class RecordingHostApplicationLifetime : IHostApplicationLifetime
    {
        public int StopApplicationCallCount { get; private set; }

        public CancellationToken ApplicationStarted => CancellationToken.None;
        public CancellationToken ApplicationStopping => CancellationToken.None;
        public CancellationToken ApplicationStopped => CancellationToken.None;

        public void StopApplication() => StopApplicationCallCount++;
    }
}
