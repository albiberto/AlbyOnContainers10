namespace AlbyOnContainers.Kernel.Persistence.Interceptors;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

        var entities = dbContext.ChangeTracker
            .Entries<AggregateRoot>()
            .Where(e => e.Entity.DomainEvents.Count != 0)
            .Select(e => e.Entity)
            .ToList();

        var domainEvents = entities.SelectMany(e => e.DomainEvents).ToList();
        if (domainEvents.Count == 0) return await base.SavingChangesAsync(eventData, result, cancellationToken);

        var scopedProvider = dbContext.GetInfrastructure();

        var publishEndpoint = scopedProvider.GetService<IPublishEndpoint>();
        if (publishEndpoint is null)
        {
            logger.LogWarning("IPublishEndpoint not available in the current scope. {Count} Domain Event(s) were NOT published.", domainEvents.Count);
            return await base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        var mapper = scopedProvider.GetService<IDomainEventMapper>();

        // 1. Publish all events to MassTransit.
        foreach (var domainEvent in domainEvents)
        {
            logger.LogDebug("Processing Domain Event: {EventName}", domainEvent.GetType().Name);

            var integrationMessages = mapper?.Map(domainEvent)?.ToList() ?? [];
            if (integrationMessages.Count == 0)
            {
                logger.LogDebug("Domain Event {EventName} produced no Integration Messages...", domainEvent.GetType().Name);
                continue;
            }

            foreach (var message in integrationMessages) 
            {
                await publishEndpoint.Publish(message, message.GetType(), cancellationToken);
            }
        }

        // 2. Commit the underlying SQL transaction (Business Entities + Outbox Messages simultaneously).
        var saveResult = await base.SavingChangesAsync(eventData, result, cancellationToken);

        // 3. Clear events from memory ONLY after a successful database commit.
        foreach (var entity in entities) entity.ClearDomainEvents();

        return saveResult;
    }
}