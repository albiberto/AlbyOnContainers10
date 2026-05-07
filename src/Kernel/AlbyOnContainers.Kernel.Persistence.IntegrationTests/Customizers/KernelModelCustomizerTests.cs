using AlbyOnContainers.Kernel.Persistence.Abstractions;
using AlbyOnContainers.Kernel.Persistence.IntegrationTests.Fakes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AlbyOnContainers.Kernel.Persistence.IntegrationTests.Customizers;

/// <summary>
/// Integration tests for the EF Core model plugin mechanism.
///
/// <para>
/// These tests verify that the entire plugin infrastructure pipeline works
/// correctly end-to-end:
/// </para>
/// <list type="number">
///   <item>The plugin (<see cref="FakeMessagingPlugin"/>) is registered in the DI container.</item>
///   <item><c>WithPersistence</c> resolves all <see cref="IModelConfigurationPlugin"/> and
///         injects them into the <c>KernelModelPluginsExtension</c> of the <c>DbContextOptions</c>.</item>
///   <item><c>KernelModelCustomizer</c> reads the extension and invokes <c>plugin.Apply(modelBuilder)</c>.</item>
///   <item>The entity configured by the plugin is actually present in the compiled DbContext model.</item>
/// </list>
/// </summary>
[TestFixture]
public sealed class KernelModelCustomizerTests : IntegrationTestBase
{
    /// <summary>
    /// Overrides the base class hook to register the <see cref="FakeMessagingPlugin"/>
    /// as an <see cref="IModelConfigurationPlugin"/> in the DI container.
    ///
    /// <para>
    /// Registration occurs <strong>before</strong> the call to <c>WithPersistence</c>
    /// (orchestrated in <see cref="IntegrationTestBase.OneTimeSetUp"/>), ensuring that the plugin
    /// is visible when EF Core builds the <c>DbContextOptions</c>.
    /// </para>
    /// </summary>
    /// <param name="services">The service collection of the host being built.</param>
    protected override void RegisterAdditionalServices(IServiceCollection services)
    {
        // Registers the dummy plugin that simulates the Messaging module.
        // Singleton because plugins are stateless and shared throughout the test duration.
        services.AddSingleton<IModelConfigurationPlugin, FakeMessagingPlugin>();
    }

    /// <summary>
    /// Verifies that the plugins registered in the DI container are applied to the EF Core model.
    ///
    /// <para>
    /// <strong>Arrange:</strong> <see cref="FakeMessagingPlugin"/> has been registered in
    /// <see cref="RegisterAdditionalServices"/> and configures <see cref="FakeOutboxMessage"/>
    /// in the <c>ModelBuilder</c>.
    /// </para>
    /// <para>
    /// <strong>Act:</strong> We resolve the <see cref="TestDbContext"/> directly from the
    /// scope created by the base class. EF Core compiles the model the first time the
    /// context is used or when we access <c>Model</c>.
    /// </para>
    /// <para>
    /// <strong>Assert:</strong> <c>DbContext.Model.FindEntityType(typeof(FakeOutboxMessage))</c>
    /// must return a non-null instance, demonstrating that:
    /// <list type="bullet">
    ///   <item>The <c>KernelModelPluginsExtension</c> transported the plugin to the customizer.</item>
    ///   <item>The <c>KernelModelCustomizer</c> invoked <c>Apply</c> correctly.</item>
    ///   <item>The <c>FakeOutboxMessage</c> entity is an integral part of the compiled EF Core model.</item>
    /// </list>
    /// </para>
    /// </summary>
    [Test]
    public void FindEntityType_WhenPluginIsRegistered_ShouldReturnConfiguredEntity()
    {
        // Arrange
        // Plugin is already registered in RegisterAdditionalServices
        
        // Act
        // Access the DbContext Model already resolved by the base class.
        // EF Core compiles the model lazily and caches it in DbContextOptions:
        // this is the moment when KernelModelCustomizer is executed.
        var entityType = DbContext.Model.FindEntityType(typeof(FakeOutboxMessage));

        // Assert
        // If entityType is not null, it means FakeMessagingPlugin.Apply() was
        // invoked by KernelModelCustomizer and the entity is in the compiled model.
        Assert.That(
            entityType,
            Is.Not.Null,
            "FakeOutboxMessage should be present in the EF Core model. " +
            "This indicates that KernelModelCustomizer correctly applied " +
            "all plugins registered via IModelConfigurationPlugin.");
    }

    /// <summary>
    /// Verifies that the table name configured by the plugin matches
    /// the one declared in <see cref="FakeMessagingPlugin"/>.
    ///
    /// <para>
    /// This test complements the previous one: it doesn't just verify
    /// the presence of the entity in the model, but also checks that the relational
    /// mapping (table name) was applied correctly.
    /// </para>
    /// </summary>
    [Test]
    public void GetTableName_WhenPluginIsRegistered_ShouldReturnMappedTableName()
    {
        // Arrange
        // Plugin is already registered in RegisterAdditionalServices
        
        // Act
        var entityType = DbContext.Model.FindEntityType(typeof(FakeOutboxMessage));
        var tableName = entityType?.GetTableName();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(entityType, Is.Not.Null, "FakeOutboxMessage not found in the EF Core model.");
            Assert.That(
                tableName,
                Is.EqualTo("fake_outbox_messages"),
                "The table name configured by the plugin does not match the expected one.");
        });
    }
}
