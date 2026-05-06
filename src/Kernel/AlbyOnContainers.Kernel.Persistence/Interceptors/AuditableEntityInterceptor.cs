namespace AlbyOnContainers.Kernel.Persistence.Interceptors;

using System;
using System.Threading;
using System.Threading.Tasks;
using Abstractions;
using Domain.SeedWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Security.Abstractions;

public sealed class AuditableEntityInterceptor(ICurrentUserService currentUserService, TimeProvider timeProvider) : SaveChangesInterceptorBase
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        throw new NotSupportedException("Synchronous DB operations are strictly forbidden. Use SaveChangesAsync instead.");
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        UpdateAuditFields(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void UpdateAuditFields(DbContext? context)
    {
        if (context is null) return;

        var currentUserId = currentUserService.UserId ?? "System";

        var transactionTimestamp = timeProvider.GetUtcNow();

        foreach (var entry in context.ChangeTracker.Entries<AuditableEntity>())
        {
            if (entry.State == EntityState.Added) 
            {
                entry.Entity.SetCreated(currentUserId, transactionTimestamp);
            }

            if (entry.State is EntityState.Added or EntityState.Modified) 
            {
                entry.Entity.SetUpdated(currentUserId, transactionTimestamp);
            }
        }
    }
}