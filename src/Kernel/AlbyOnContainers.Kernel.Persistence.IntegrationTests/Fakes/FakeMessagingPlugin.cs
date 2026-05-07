using AlbyOnContainers.Kernel.Persistence.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace AlbyOnContainers.Kernel.Persistence.IntegrationTests.Fakes;

/// <summary>
/// Dummy plugin that simulates what an external module (e.g., Messaging) would do
/// to inject its own table into the <see cref="DbContext"/> via the
/// <see cref="IModelConfigurationPlugin"/> mechanism.
///
/// Its only purpose is to register <see cref="FakeOutboxMessage"/> in the
/// EF Core model, allowing the test to verify that:
/// 1. The plugin has been resolved by the DI container.
/// 2. The <see cref="KernelModelCustomizer"/> has invoked it correctly.
/// 3. The entity is actually present in the compiled DbContext model.
/// </summary>
public sealed class FakeMessagingPlugin : IModelConfigurationPlugin
{
    /// <inheritdoc />
    public void Apply(ModelBuilder modelBuilder)
    {
        // Configures the dummy table that simulates Outbox Messages.
        // In a real plugin (e.g., MessagingPlugin), this section
        // would configure the OutboxMessages table with all its columns.
        modelBuilder.Entity<FakeOutboxMessage>(entity =>
        {
            // Primary key based on the Guid Id
            entity.HasKey(e => e.Id);

            // Maps to the "fake_outbox_messages" table (snake_case by convention)
            entity.ToTable("fake_outbox_messages");

            // The payload cannot be null and has a reasonable maximum length
            entity.Property(e => e.Payload)
                .IsRequired()
                .HasMaxLength(4096);
        });
    }
}
