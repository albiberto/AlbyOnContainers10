namespace AlbyOnContainers.Kernel.Persistence.Interceptors;

using Domain.SeedWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Security.Abstractions;

public sealed class AuditableEntityInterceptor(
    ILogger<AuditableEntityInterceptor> logger,
    ICurrentUserService? currentUserService = null) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result) =>
        throw new InvalidOperationException("Synchronous SaveChanges is strictly prohibited. You MUST use SaveChangesAsync().");

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        var dbContext = eventData.Context;
        if (dbContext is null)
        {
            logger.LogWarning("AuditableEntityInterceptor invoked without a DbContext. Skipping audit metadata write.");
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }


        var userId = currentUserService?.UserId ?? "System";

        foreach (var entry in dbContext.ChangeTracker.Entries<AuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.SetCreationInfo(userId);
                    break;
                case EntityState.Modified:
                    entry.Entity.SetUpdateInfo(userId);
                    break;
            }
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}