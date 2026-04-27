using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AlbyOnContainers.Kernel.Domain.SeedWork;
using MassTransit;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AlbyOnContainers.Kernel.Persistence.Interceptors;

// Il costruttore ora è pulito e coerente: riceve solo il logger.
public sealed class DomainEventDispatcherInterceptor(ILogger<DomainEventDispatcherInterceptor> logger) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        throw new InvalidOperationException("Synchronous SaveChanges is strictly prohibited. You MUST use SaveChangesAsync() to ensure Domain Events are dispatched correctly without blocking threads.");
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        var dbContext = eventData.Context;
        
        if (dbContext is null) return await base.SavingChangesAsync(eventData, result, cancellationToken);

        var entities = dbContext.ChangeTracker
            .Entries<AggregateRoot>()
            .Where(e => e.Entity.DomainEvents.Count != 0)
            .Select(e => e.Entity)
            .ToList();

        var domainEvents = entities.SelectMany(e => e.DomainEvents).ToList();

        foreach (var entity in entities) 
        {
            entity.ClearDomainEvents();
        }

        if (domainEvents.Count == 0) return await base.SavingChangesAsync(eventData, result, cancellationToken);

        // Estraiamo lo scope dal DbContext una sola volta
        var scopedProvider = dbContext.GetInfrastructure();

        // 1. Risolviamo il Publish Endpoint. Se manca, logghiamo una sola volta e usciamo.
        var publishEndpoint = scopedProvider.GetService<IPublishEndpoint>();
        if (publishEndpoint is null)
        {
            logger.LogWarning("IPublishEndpoint not available in the current scope. {Count} Domain Event(s) were NOT published.", domainEvents.Count);
            return await base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        // 2. Risolviamo il Mapper coerentemente dallo stesso provider
        var mapper = scopedProvider.GetService<IDomainEventMapper>();

        foreach (var domainEvent in domainEvents)
        {
            logger.LogDebug("Dispatching Domain Event: {EventName}", domainEvent.GetType().Name);

            var integrationMessage = mapper?.Map(domainEvent);

            if (integrationMessage is null) 
            {
                logger.LogDebug("Domain Event {EventName} was not mapped to an Integration Message and will be ignored.", domainEvent.GetType().Name);
                continue;
            }
            
            await publishEndpoint.Publish(integrationMessage, cancellationToken);
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}