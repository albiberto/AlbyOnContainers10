namespace AlbyOnContainers.Kernel.Persistence.Interceptors;

using Abstractions;
using Domain.SeedWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Security.Abstractions;

public sealed class AuditableInterceptor : SaveChangesInterceptorBase
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        UpdateAuditFields(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void UpdateAuditFields(DbContext? context)
    {
        if (context is null) return;

        var currentUserService = context.GetService<ICurrentUserService>();
        var currentUserId = currentUserService.UserId ?? "System";

        var transactionTimestamp = DateTimeOffset.UtcNow;

        foreach (var entry in context.ChangeTracker.Entries<AuditableEntity>())
        {
            if (entry.State == EntityState.Added) entry.Entity.SetCreated(currentUserId, transactionTimestamp);

            if (entry.State is EntityState.Added or EntityState.Modified) entry.Entity.SetUpdated(currentUserId, transactionTimestamp);
        }
    }
}