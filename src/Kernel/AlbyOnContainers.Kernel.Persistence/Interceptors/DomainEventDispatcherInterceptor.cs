namespace AlbyOnContainers.Kernel.Persistence.Interceptors;

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abstractions;
using Domain.SeedWork;
using MassTransit;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;
using Resilience.Enums;

public sealed partial class DomainEventDispatcherInterceptor(ILogger<DomainEventDispatcherInterceptor> logger, [FromKeyedServices(ResilienceKey.Messaging)] ResiliencePipeline pipeline) : SaveChangesInterceptorBase
{
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        // Guard: if the context is unavailable, skip processing.
        var dbContext = eventData.Context;
        if (dbContext is null) return await base.SavingChangesAsync(eventData, result, cancellationToken);

        // Phase 1 — Discovery: find all tracked AggregateRoots that have raised at least one domain event.
        var entitiesWithEvents = dbContext.ChangeTracker
            .Entries<AggregateRoot>()
            .Where(e => e.Entity.DomainEvents.Count != 0)
            .Select(e => e.Entity)
            .ToList();

        // Short-circuit: nothing to dispatch, proceed normally.
        if (entitiesWithEvents.Count == 0) return await base.SavingChangesAsync(eventData, result, cancellationToken);

        // Phase 2 — Collection: flatten all domain events into a single list.
        // Events are intentionally NOT cleared yet — if publishing fails, the entities
        // retain their events and no data is silently lost.
        var domainEvents = entitiesWithEvents
            .SelectMany(e => e.DomainEvents)
            .ToList();

        // Resolve infrastructure services from the DbContext's service provider.
        var publishEndpoint = dbContext.GetService<IPublishEndpoint>();
        var mapper = dbContext.GetService<IDomainEventMapper>();

        // Phase 3 — Mapping & Publishing: translate each domain event into zero or more
        // integration messages and publish them to the message broker.
        // Each publish is individually wrapped in the messaging resilience pipeline
        // to handle transient broker failures with retry and timeout.
        foreach (var domainEvent in domainEvents)
        {
            var eventName = domainEvent.GetType().Name;

            LogProcessingDomainEvent(eventName);

            // Map the domain event to integration messages.
            // If the mapper produces no messages, the event is skipped.
            var integrationMessages = mapper.Map(domainEvent).ToList();

            if (integrationMessages.Count == 0)
            {
                LogNoIntegrationMessagesProduced(eventName);
                continue;
            }

            foreach (var message in integrationMessages)
            {
                await pipeline.ExecuteAsync(
                    static async (state, token) => await state.Endpoint.Publish(state.Message, state.Message.GetType(), token),
                    (Endpoint: publishEndpoint, Message: message),
                    cancellationToken);
            }
        }

        // Phase 4 — Cleanup: clear domain events only after all messages have been
        // successfully published. This ensures events survive any exception thrown
        // during the publishing phase.
        foreach (var entity in entitiesWithEvents) entity.ClearDomainEvents();

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}