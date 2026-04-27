namespace AlbyOnContainers.Kernel.Persistence.Interceptors;

using Domain.SeedWork;
using Security.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;

public sealed class AuditableEntityInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        // FAIL-FAST: Synchronous I/O is strictly forbidden in this architecture.
        // If a developer calls DbContext.SaveChanges(), the application will crash explicitly here.
        throw new InvalidOperationException("Synchronous SaveChanges is strictly prohibited. You MUST use SaveChangesAsync().");
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        var dbContext = eventData.Context;
        if (dbContext is null) return base.SavingChangesAsync(eventData, result, cancellationToken);

        var currentUserService = dbContext.GetService<ICurrentUserService>();
        var userId = currentUserService.UserName ?? "System";

        var entries = dbContext.ChangeTracker.Entries<AuditableEntity>();

        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Added) entry.Entity.SetCreationInfo(userId);

            if (entry.State is not (EntityState.Added or EntityState.Modified)) continue;
            
            entry.Entity.SetUpdateInfo(userId);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}