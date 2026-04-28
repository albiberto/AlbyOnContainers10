namespace AlbyOnContainers.Kernel.Persistence.Interceptors;

using Domain.SeedWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Security.Abstractions;

public sealed class AuditableEntityInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result) => throw new InvalidOperationException("Synchronous SaveChanges is strictly prohibited. You MUST use SaveChangesAsync().");

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        var dbContext = eventData.Context;
        if (dbContext is null) return base.SavingChangesAsync(eventData, result, cancellationToken);

        var scopedProvider = dbContext.GetInfrastructure();
        var currentUserService = scopedProvider.GetService<ICurrentUserService>();

        var userId = currentUserService?.UserId ?? "System";

        var entries = dbContext.ChangeTracker.Entries<AuditableEntity>();

        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Added) entry.Entity.SetCreationInfo(userId);
            if (entry.State == EntityState.Modified) entry.Entity.SetUpdateInfo(userId);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}