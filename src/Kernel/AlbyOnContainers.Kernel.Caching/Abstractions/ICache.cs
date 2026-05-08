namespace AlbyOnContainers.Kernel.Caching.Abstractions;

public interface ICache
{
    Task<T?> GetOrSetAsync<T>(IKey key, Func<CancellationToken, Task<T>> factory, CancellationToken ct = default);

    Task RemoveAsync(IKey key, CancellationToken ct = default);

    Task SetAsync<T>(IKey key, T value, CancellationToken ct = default);
}
