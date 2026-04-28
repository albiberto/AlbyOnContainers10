namespace AlbyOnContainers.Kernel.Persistence.Interceptors;

using Domain.SeedWork;
using MassTransit;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

public sealed class DomainEventDispatcherInterceptor(ILogger<DomainEventDispatcherInterceptor> logger) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result) => throw new InvalidOperationException("Synchronous SaveChanges is strictly prohibited. You MUST use SaveChangesAsync() to ensure Domain Events are dispatched correctly without blocking threads.");

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        var dbContext = eventData.Context;
        if (dbContext is null) return await base.SavingChangesAsync(eventData, result, cancellationToken);

        // 1. Identifica gli aggregati con eventi
        var entitiesWithEvents = dbContext.ChangeTracker
            .Entries<AggregateRoot>()
            .Where(e => e.Entity.DomainEvents.Count != 0)
            .Select(e => e.Entity)
            .ToList();

        if (entitiesWithEvents.Count == 0) return await base.SavingChangesAsync(eventData, result, cancellationToken);

        // 2. ESTRAZIONE E PULIZIA IMMEDIATA (Idempotenza garantita)
        // Se SavingChanges fallisce e viene richiamato, non ri-processeremo questi eventi.
        var domainEvents = entitiesWithEvents
            .SelectMany(e =>
            {
                var events = e.DomainEvents.ToList();
                e.ClearDomainEvents();
                return events;
            })
            .ToList();

        var scopedProvider = dbContext.GetInfrastructure();
        var publishEndpoint = scopedProvider.GetService<IPublishEndpoint>();
        var mapper = scopedProvider.GetService<IDomainEventMapper>();

        if (publishEndpoint is null)
        {
            logger.LogWarning("IPublishEndpoint not available. {Count} events cleared but not published.", domainEvents.Count);
            return await base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        // 3. Pubblicazione nell'Outbox (aggiunge entità al ChangeTracker)
        foreach (var domainEvent in domainEvents)
        {
            var integrationMessages = mapper?.Map(domainEvent)?.ToList() ?? [];
            foreach (var message in integrationMessages) await publishEndpoint.Publish(message, message.GetType(), cancellationToken);
        }

        // 4. Esecuzione del commit SQL (Entità Business + Righe Outbox in un'unica transazione)
        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}