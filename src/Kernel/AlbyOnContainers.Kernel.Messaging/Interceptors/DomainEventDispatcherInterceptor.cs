namespace AlbyOnContainers.Kernel.Messaging.Interceptors;

using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Domain.SeedWork;
using Persistence.Abstractions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

public sealed partial class DomainEventDispatcherInterceptor(
    ILogger<DomainEventDispatcherInterceptor> logger,
    IPublishEndpoint publishEndpoint,
    IDomainEventMapper mapper) : SaveChangesInterceptorBase
{
    private readonly ConcurrentDictionary<Guid, List<AggregateRoot>> _pendingEventCleanup = new();

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        var dbContext = eventData.Context;
        if (dbContext is null) return await base.SavingChangesAsync(eventData, result, cancellationToken);

        var entitiesWithEvents = dbContext.ChangeTracker
            .Entries<AggregateRoot>()
            .Where(e => e.Entity.DomainEvents.Count != 0)
            .Select(e => e.Entity)
            .ToList();

        if (entitiesWithEvents.Count == 0) return await base.SavingChangesAsync(eventData, result, cancellationToken);

        var domainEvents = entitiesWithEvents
            .SelectMany(e => e.DomainEvents)
            .ToList();

        foreach (var domainEvent in domainEvents)
        {
            var eventName = domainEvent.GetType().Name;

            LogProcessingDomainEvent(eventName);

            var integrationMessages = mapper.Map(domainEvent).ToList();

            if (integrationMessages.Count == 0)
            {
                LogNoIntegrationMessagesProduced(eventName);
                continue;
            }

            foreach (var message in integrationMessages)
            {
                // With MassTransit Outbox enabled this writes to the DbContext outbox,
                // sharing the same ACID transaction as the business data.
                await publishEndpoint.Publish(message, message.GetType(), cancellationToken);
            }
        }

        // Clear only after EF confirms SaveChangesAsync succeeded. If the DB write or
        // outbox transaction fails, the events remain on the aggregate for a retry.
        _pendingEventCleanup[dbContext.ContextId.InstanceId] = entitiesWithEvents;

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        ClearPendingEvents(eventData.Context);
        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        DropPendingCleanup(eventData.Context);
        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    public override Task SaveChangesCanceledAsync(DbContextEventData eventData, CancellationToken cancellationToken = default)
    {
        DropPendingCleanup(eventData.Context);
        return base.SaveChangesCanceledAsync(eventData, cancellationToken);
    }

    private void ClearPendingEvents(DbContext? context)
    {
        if (context is null) return;

        if (!_pendingEventCleanup.TryRemove(context.ContextId.InstanceId, out var entities)) return;

        foreach (var entity in entities) entity.ClearDomainEvents();
    }

    private void DropPendingCleanup(DbContext? context)
    {
        if (context is null) return;

        _pendingEventCleanup.TryRemove(context.ContextId.InstanceId, out _);
    }
}
