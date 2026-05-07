using AlbyOnContainers.Kernel.Persistence.Abstractions;
using AlbyOnContainers.Kernel.Persistence.IntegrationTests.Fakes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AlbyOnContainers.Kernel.Persistence.IntegrationTests.Customizers;

/// <summary>
/// Test di integrazione per il meccanismo di plugin del modello EF Core.
///
/// <para>
/// Questi test verificano che l'intera pipeline infrastrutturale dei plugin funzioni
/// correttamente end-to-end:
/// </para>
/// <list type="number">
///   <item>Il plugin (<see cref="FakeMessagingPlugin"/>) viene registrato nel container DI.</item>
///   <item><c>WithPersistence</c> risolve tutti i <see cref="IModelConfigurationPlugin"/> e
///         li inietta nella <c>KernelModelPluginsExtension</c> delle <c>DbContextOptions</c>.</item>
///   <item><c>KernelModelCustomizer</c> legge l'extension e invoca <c>plugin.Apply(modelBuilder)</c>.</item>
///   <item>L'entità configurata dal plugin è effettivamente presente nel modello compilato del DbContext.</item>
/// </list>
/// </summary>
[TestFixture]
public sealed class KernelModelCustomizerTests : IntegrationTestBase
{
    /// <summary>
    /// Sovrascrive l'hook della base class per registrare il <see cref="FakeMessagingPlugin"/>
    /// come <see cref="IModelConfigurationPlugin"/> nel container DI.
    ///
    /// <para>
    /// La registrazione avviene <strong>prima</strong> della chiamata a <c>WithPersistence</c>
    /// (orchestrata in <see cref="IntegrationTestBase.OneTimeSetUp"/>), garantendo che il plugin
    /// sia visibile quando EF Core costruisce le <c>DbContextOptions</c>.
    /// </para>
    /// </summary>
    /// <param name="services">La collezione di servizi dell'host in costruzione.</param>
    protected override void RegisterAdditionalServices(IServiceCollection services)
    {
        // Registra il plugin fasullo che simula il modulo Messaging.
        // Singleton perché i plugin sono stateless e condivisi per tutta la durata del test.
        services.AddSingleton<IModelConfigurationPlugin, FakeMessagingPlugin>();
    }

    /// <summary>
    /// Verifica che i plugin registrati nel container DI vengano applicati al modello EF Core.
    ///
    /// <para>
    /// <strong>Arrange:</strong> <see cref="FakeMessagingPlugin"/> è stato registrato in
    /// <see cref="RegisterAdditionalServices"/> e configura <see cref="FakeOutboxMessage"/>
    /// nel <c>ModelBuilder</c>.
    /// </para>
    /// <para>
    /// <strong>Act:</strong> Risolviamo il <see cref="TestDbContext"/> direttamente dallo
    /// scope creato dalla base class. EF Core compila il modello la prima volta che il
    /// contesto viene usato o quando accediamo a <c>Model</c>.
    /// </para>
    /// <para>
    /// <strong>Assert:</strong> <c>DbContext.Model.FindEntityType(typeof(FakeOutboxMessage))</c>
    /// deve restituire un'istanza non nulla, dimostrando che:
    /// <list type="bullet">
    ///   <item>La <c>KernelModelPluginsExtension</c> ha trasportato il plugin fino al customizer.</item>
    ///   <item>Il <c>KernelModelCustomizer</c> ha invocato correttamente <c>Apply</c>.</item>
    ///   <item>L'entità <c>FakeOutboxMessage</c> è parte integrante del modello EF Core compilato.</item>
    /// </list>
    /// </para>
    /// </summary>
    [Test]
    public void FindEntityType_WhenPluginIsRegistered_ShouldReturnConfiguredEntity()
    {
        // Act
        // Accediamo al Model del DbContext già risolto dalla base class.
        // EF Core compila il modello in modo lazy e lo cachea nelle DbContextOptions:
        // questo è il momento in cui KernelModelCustomizer viene eseguito.
        var entityType = DbContext.Model.FindEntityType(typeof(FakeOutboxMessage));

        // Assert
        // Se entityType non è null, significa che FakeMessagingPlugin.Apply() è stato
        // invocato da KernelModelCustomizer e l'entità è nel modello compilato.
        Assert.That(
            entityType,
            Is.Not.Null,
            "FakeOutboxMessage dovrebbe essere presente nel modello EF Core. " +
            "Questo indica che KernelModelCustomizer ha correttamente applicato " +
            "tutti i plugin registrati tramite IModelConfigurationPlugin.");
    }

    /// <summary>
    /// Verifica che il nome della tabella configurata dal plugin corrisponda
    /// a quello dichiarato in <see cref="FakeMessagingPlugin"/>.
    ///
    /// <para>
    /// Questo test è complementare al precedente: non si limita a verificare
    /// la presenza dell'entità nel modello, ma controlla anche che la mappatura
    /// relazionale (nome tabella) sia stata applicata correttamente.
    /// </para>
    /// </summary>
    [Test]
    public void GetTableName_WhenPluginIsRegistered_ShouldReturnMappedTableName()
    {
        // Act
        var entityType = DbContext.Model.FindEntityType(typeof(FakeOutboxMessage));

        // Assert — prerequisito: l'entità deve esistere nel modello
        Assert.That(entityType, Is.Not.Null, "FakeOutboxMessage non trovato nel modello EF Core.");

        var tableName = entityType!.GetTableName();

        Assert.That(
            tableName,
            Is.EqualTo("fake_outbox_messages"),
            "Il nome della tabella configurato dal plugin non corrisponde a quello atteso.");
    }
}
