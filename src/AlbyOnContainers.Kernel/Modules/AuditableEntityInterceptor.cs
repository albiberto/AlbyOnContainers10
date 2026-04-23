using AlbyOnContainers.Shared.Application.Abstract;
using AlbyOnContainers.Kernel.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using AlbyOnContainers.Kernel.Domain.SeedWork;

namespace AlbyOnContainers.Kernel.Modules;

/// <summary>
/// Interceptor to automatically populate audit fields (CreatedBy, CreatedAt, UpdatedBy, UpdatedAt)
/// for entities inheriting from AuditableEntity.
/// </summary>
public sealed class AuditableEntityInterceptor(ICurrentUserService currentUserService) : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is null) return base.SavingChangesAsync(eventData, result, cancellationToken);

        var entries = eventData.Context.ChangeTracker.Entries<AuditableEntity>();
        
        var userId = currentUserService.UserId ?? "System";

        foreach (var entry in entries)
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
