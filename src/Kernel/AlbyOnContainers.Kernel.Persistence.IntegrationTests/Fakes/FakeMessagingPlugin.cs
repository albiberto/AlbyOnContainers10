using AlbyOnContainers.Kernel.Persistence.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace AlbyOnContainers.Kernel.Persistence.IntegrationTests.Fakes;

/// <summary>
/// Plugin fasullo che simula ciò che farebbe un modulo esterno (es. Messaging)
/// per iniettare la propria tabella nel <see cref="DbContext"/> tramite il
/// meccanismo di <see cref="IModelConfigurationPlugin"/>.
///
/// Il suo unico scopo è registrare <see cref="FakeOutboxMessage"/> nel modello
/// EF Core, permettendo al test di verificare che:
/// 1. Il plugin sia stato risolto dal container DI.
/// 2. Il <see cref="KernelModelCustomizer"/> lo abbia invocato correttamente.
/// 3. L'entità sia effettivamente presente nel modello compilato del DbContext.
/// </summary>
public sealed class FakeMessagingPlugin : IModelConfigurationPlugin
{
    /// <inheritdoc />
    public void Apply(ModelBuilder modelBuilder)
    {
        // Configura la tabella fittizia che simula un Outbox Messages.
        // In un plugin reale (es. MessagingPlugin), questa sezione
        // configurerebbe la tabella OutboxMessages con tutte le sue colonne.
        modelBuilder.Entity<FakeOutboxMessage>(entity =>
        {
            // Chiave primaria basata sul Guid Id
            entity.HasKey(e => e.Id);

            // Mappa alla tabella "fake_outbox_messages" (snake_case come convenzione)
            entity.ToTable("fake_outbox_messages");

            // Il payload non può essere nullo e ha una lunghezza massima ragionevole
            entity.Property(e => e.Payload)
                .IsRequired()
                .HasMaxLength(4096);
        });
    }
}
