namespace AlbyOnContainers.Kernel.Persistence.Interceptors;

using Domain.SeedWork;
using MassTransit;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

public sealed class DomainEventDispatcherInterceptor(ILogger<DomainEventDispatcherInterceptor> logger) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result) =>
        throw new InvalidOperationException("Synchronous SaveChanges is strictly prohibited. You MUST use SaveChangesAsync() to ensure Domain Events are dispatched correctly without blocking threads.");

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        var dbContext = eventData.Context;
        if (dbContext is null) return await base.SavingChangesAsync(eventData, result, cancellationToken);

        // 1. Identifica gli aggregati con eventi.
        var entitiesWithEvents = dbContext.ChangeTracker
            .Entries<AggregateRoot>()
            .Where(e => e.Entity.DomainEvents.Count != 0)
            .Select(e => e.Entity)
            .ToList();

        if (entitiesWithEvents.Count == 0) return await base.SavingChangesAsync(eventData, result, cancellationToken);

        // 2. EXTRACT & CLEAR PRE-COMMIT.
        // Rationale:
        //   - With EF Outbox enabled, publishEndpoint.Publish writes outbox rows in the SAME
        //     transaction as the aggregate. If SaveChangesAsync fails, both are rolled back at SQL level.
        //   - Clearing in-memory events before SaveChanges is therefore safe for OUTBOX scenarios:
        //     even if SaveChanges fails, no event reaches the broker (outbox rows are rolled back),
        //     and the aggregate is no longer in memory after the unit-of-work ends.
        //   - For NON-OUTBOX scenarios (publish-direct), this becomes at-most-once on retry: the
        //     in-memory events are lost on the second SaveChanges attempt. ALWAYS use the outbox.
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

        // 3. Pubblicazione nell'Outbox (aggiunge entità OutboxMessage al ChangeTracker).
        foreach (var domainEvent in domainEvents)
        {
            logger.LogDebug("Processing Domain Event: {EventName}", domainEvent.GetType().Name);

            var integrationMessages = mapper?.Map(domainEvent)?.ToList() ?? [];

            if (integrationMessages.Count == 0)
            {
                logger.LogDebug("Domain Event {EventName} produced no Integration Messages.", domainEvent.GetType().Name);
                continue;
            }

            foreach (var message in integrationMessages)
                await publishEndpoint.Publish(message, message.GetType(), cancellationToken);
        }

        // 4. Esecuzione del commit SQL (entità business + righe outbox in un'unica transazione).
        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}