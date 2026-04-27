using AlbyOnContainers.Kernel.Domain.SeedWork;
using MassTransit;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AlbyOnContainers.Kernel.Persistence.Interceptors;

public sealed class DomainEventDispatcherInterceptor(
    IServiceProvider serviceProvider,
    ILogger<DomainEventDispatcherInterceptor> logger) : SaveChangesInterceptor
{
    // ==============================================================================
    // SYNCHRONOUS PATH (BLOCKED)
    // ==============================================================================

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        // FAIL-FAST: Synchronous I/O is strictly forbidden in this architecture.
        // If a developer calls DbContext.SaveChanges(), the application will crash explicitly here.
        throw new InvalidOperationException("Synchronous SaveChanges is strictly prohibited. You MUST use SaveChangesAsync() to ensure Domain Events are dispatched correctly without blocking threads.");
    }

    // ==============================================================================
    // ASYNCHRONOUS PATH
    // ==============================================================================

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        var dbContext = eventData.Context;
        
        if (dbContext is null) return await base.SavingChangesAsync(eventData, result, cancellationToken);

        // 1. Collect all AggregateRoots that contain Domain Events
        var entities = dbContext.ChangeTracker
            .Entries<AggregateRoot>()
            .Where(e => e.Entity.DomainEvents.Count != 0)
            .Select(e => e.Entity)
            .ToList();

        // 2. Extract the events
        var domainEvents = entities.SelectMany(e => e.DomainEvents).ToList();

        // 3. MANDATORY: Clear events BEFORE dispatching to prevent double-dispatch 
        // in case Entity Framework Core triggers an Execution Strategy Retry.
        foreach (var entity in entities) 
        {
            entity.ClearDomainEvents();
        }

        // Exit early if there are no events to process
        if (domainEvents.Count == 0) return await base.SavingChangesAsync(eventData, result, cancellationToken);

        // 4. Resolve the mapper dynamically from the Service Provider
        var mapper = serviceProvider.GetService<IDomainEventMapper>();

        foreach (var domainEvent in domainEvents)
        {
            logger.LogDebug("Dispatching Domain Event: {EventName}", domainEvent.GetType().Name);

            // 5. If a mapper is available, map the Domain Event to an Integration Message (Command/Event)
            var integrationMessage = mapper?.Map(domainEvent);

            if (integrationMessage == null) continue;
            
            var publishEndpoint = serviceProvider.GetRequiredService<IPublishEndpoint>();
            await publishEndpoint.Publish(integrationMessage, cancellationToken);
        }

        // 7. Continue with the standard EF Core Save pipeline
        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}