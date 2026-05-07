namespace AlbyOnContainers.Kernel.Caching.Cache;

public interface IAlbyCache
{
    Task<T?> GetOrSetAsync<T>(string key, Func<CancellationToken, Task<T>> factory, CancellationToken ct);

    Task RemoveAsync(string key, CancellationToken ct);

    Task SetAsync<T>(string key, T value, CancellationToken ct);
}
