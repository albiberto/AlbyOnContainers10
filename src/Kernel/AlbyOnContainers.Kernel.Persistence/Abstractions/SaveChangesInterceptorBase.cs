namespace AlbyOnContainers.Kernel.Persistence.Abstractions;

using Microsoft.EntityFrameworkCore.Diagnostics;

public abstract class SaveChangesInterceptorBase : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result) => throw new InvalidOperationException($"{GetType().Name} strictly prohibits synchronous operations. You MUST use SaveChangesAsync() to ensure correct event dispatching and prevent thread pool starvation.");
}