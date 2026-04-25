using AlbyOnContainers.Kernel.Domain.SeedWork;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;

namespace AlbyOnContainers.Kernel.Persistence;

/// <summary>
/// A SaveChanges interceptor that automatically dispatches domain events
/// accumulated by AggregateRoot entities to the MassTransit bus before
/// the transaction commits. When an EF Core Outbox is configured, the
/// publish is transactionally consistent with the database write.
/// </summary>
public sealed class DomainEventDispatcherInterceptor(
    ILogger<DomainEventDispatcherInterceptor> logger) : SaveChangesInterceptor
{
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
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

        // Collect and clear before publish — prevents re-dispatch on retry
        var domainEvents = aggregates
            .SelectMany(a => a.DomainEvents)
            .ToList();

        foreach (var aggregate in aggregates)
        {
            aggregate.ClearDomainEvents();
        }

        // Resolve IPublishEndpoint dynamically from the DbContext's service provider.
        // This works within a MassTransit consumer scope, and when the EF Core Outbox
        // is active, publish is enlisted in the same transaction as SaveChanges.
        var publishEndpoint = context.GetService<IPublishEndpoint>();

        foreach (var domainEvent in domainEvents)
        {
            logger.LogDebug(
                "Dispatching domain event {EventType} from interceptor.",
                domainEvent.GetType().Name);

            await publishEndpoint.Publish(domainEvent, domainEvent.GetType(), cancellationToken);
        }
    }
}
