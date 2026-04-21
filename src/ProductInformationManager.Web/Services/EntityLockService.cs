using ProductInformationManager.Web.Notifiers;
using ZiggyCreatures.Caching.Fusion;

namespace ProductInformationManager.Web.Services;

// Le strutture dati per la gestione del Lock
public record LockInfo(string LockedByUserName, DateTime AcquiredAt);
public record LockResult(bool Success, string? LockedBy);

public class EntityLockService(IFusionCache cache, LockNotifier notifier)
{
    // Durata massima del lock se l'utente sparisce senza cliccare "Cancel"
    private readonly TimeSpan _lockDuration = TimeSpan.FromMinutes(15); 
    
    // Il semaforo impedisce le "Race Condition" se due richieste arrivano allo stesso esatto millisecondo
    private static readonly SemaphoreSlim Semaphore = new(1, 1);

    public async Task<LockResult> TryAcquireLockAsync(string entityType, Guid entityId, string userName, CancellationToken ct = default)
    {
        var key = $"pim:lock:{entityType}:{entityId}";

        await Semaphore.WaitAsync(ct);
        try
        {
            // Leggiamo la cache per vedere se il lock esiste già
            var existingLock = await cache.GetOrDefaultAsync<LockInfo>(key, token: ct);

            if (existingLock is not null)
            {
                if (existingLock.LockedByUserName == userName)
                {
                    // L'utente aveva già il lock (es. ha ricaricato la pagina). Lo rinnoviamo.
                    await cache.SetAsync(key, new LockInfo(userName, DateTime.UtcNow), _lockDuration, token: ct);
                    return new LockResult(true, null);
                }
                
                // Fallimento: Entità bloccata da un altro utente!
                return new LockResult(false, existingLock.LockedByUserName);
            }

            // Nessun lock presente, lo acquisiamo per questo utente
            await cache.SetAsync(key, new LockInfo(userName, DateTime.UtcNow), _lockDuration, token: ct);
        }
        finally
        {
            Semaphore.Release(); // Sblocchiamo il semaforo per le altre richieste
        }

        // Fire & Forget: Avvisiamo istantaneamente tutta la UI (Blazor) che lo stato del lock è cambiato!
        _ = Task.Run(() => notifier.NotifyChanged(entityId), ct);

        return new LockResult(true, null);
    }

    public async Task ReleaseLockAsync(string entityType, Guid entityId, string userName, CancellationToken ct = default)
    {
        var key = $"pim:lock:{entityType}:{entityId}";

        await Semaphore.WaitAsync(ct);
        try
        {
            // Verifichiamo che il lock esista e appartenga davvero a chi lo sta liberando
            var existingLock = await cache.GetOrDefaultAsync<LockInfo>(key, token: ct);
            if (existingLock is not null && existingLock.LockedByUserName == userName)
            {
                await cache.RemoveAsync(key, token: ct);
            }
        }
        finally
        {
            Semaphore.Release();
        }

        // Fire & Forget: Avvisiamo la UI che il lock è stato liberato
        _ = Task.Run(() => notifier.NotifyChanged(entityId), ct);
    }

    // Metodo utile per la UI per disegnare il "Lucchetto" e il nome dell'utente che sta editando
    public async Task<string?> GetLockOwnerAsync(string entityType, Guid entityId, CancellationToken ct = default)
    {
        var key = $"pim:lock:{entityType}:{entityId}";
        var existingLock = await cache.GetOrDefaultAsync<LockInfo>(key, token: ct);
        return existingLock?.LockedByUserName;
    }
}