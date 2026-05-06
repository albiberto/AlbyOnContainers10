using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AlbyOnContainers.Kernel.Persistence.Abstractions;

public abstract class SaveChangesInterceptorBase : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result) => throw new NotSupportedException("Synchronous saves are not supported. Use SaveChangesAsync instead.");

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result) => throw new NotSupportedException("Synchronous saves are not supported. Use SaveChangesAsync instead.");
}