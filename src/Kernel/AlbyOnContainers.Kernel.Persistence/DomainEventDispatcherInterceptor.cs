using AlbyOnContainers.Kernel.Domain.SeedWork;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;

namespace AlbyOnContainers.Kernel.Persistence;

/// <summary>
/// A SaveChanges interceptor that automatically collects domain events from tracked
/// AggregateRoot entities, translates them to integration events via a registered
/// <see cref="IDomainEventMapper"/>, and publishes them through MassTransit before
/// the transaction commits. When the EF Core Outbox is configured, the publish
/// is transactionally durable.
///
/// The interceptor is Singleton — it resolves IPublishEndpoint and IDomainEventMapper
/// dynamically per-call from the DbContext's scoped service provider to remain
/// thread-safe and dependency-injection-clean.
/// </summary>
public sealed class DomainEventDispatcherInterceptor(ILogger<DomainEventDispatcherInterceptor> logger) : SaveChangesInterceptor
{
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            await DispatchDomainEventsAsync(eventData.Context, cancellationToken);
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private async Task DispatchDomainEventsAsync(DbContext context, CancellationToken cancellationToken)
    {
        // Gather all aggregates that have pending domain events
        var aggregates = context.ChangeTracker
            .Entries<AggregateRoot>()
            .Where(e => e.Entity.DomainEvents.Count != 0)
            .Select(e => e.Entity)
            .ToList();

        if (aggregates.Count == 0)
        {
            return;
        }

        // Collect then clear immediately — prevents re-dispatch on EF Core retry
        var domainEvents = aggregates
            .SelectMany(a => a.DomainEvents)
            .ToList();

        foreach (var aggregate in aggregates)
        {
            aggregate.ClearDomainEvents();
        }

        // Resolve MassTransit endpoint and optional domain-to-integration mapper
        // from the DbContext's scoped service provider (avoids circular deps / leaks).
        var publishEndpoint = context.GetService<IPublishEndpoint>();
        if (publishEndpoint is null)
        {
            logger.LogWarning(
                "DomainEventDispatcherInterceptor: IPublishEndpoint not available in the DbContext " +
                "service provider. {Count} domain event(s) will NOT be dispatched.",
                domainEvents.Count);
            return;
        }

        // Optional mapper: translates domain events → integration events.
        // If none is registered the domain event is published directly (useful for
        // bounded contexts where the domain event IS the integration event).
        var mapper = context.GetService<IDomainEventMapper>();

        foreach (var domainEvent in domainEvents)
        {
            if (mapper is not null)
            {
                // Map domain → integration events and publish each one
                var integrationEvents = mapper.Map(domainEvent).ToList();

                if (integrationEvents.Count == 0)
                {
                    logger.LogDebug("DomainEventDispatcherInterceptor: {EventType} produced no integration events — skipping.", domainEvent.GetType().Name);
                    continue;
                }

                foreach (var integrationEvent in integrationEvents)
                {
                    logger.LogDebug("Dispatching integration event {IntegrationEventType} (from domain event {DomainEventType}).", integrationEvent.GetType().Name, domainEvent.GetType().Name);
                    await publishEndpoint.Publish(integrationEvent, integrationEvent.GetType(), cancellationToken);
                }
            }
            else
            {
                // No mapper registered — publish the domain event directly
                logger.LogDebug("Dispatching domain event {EventType} directly (no IDomainEventMapper registered).", domainEvent.GetType().Name);
                await publishEndpoint.Publish(domainEvent, domainEvent.GetType(), cancellationToken);
            }
        }
    }
}
