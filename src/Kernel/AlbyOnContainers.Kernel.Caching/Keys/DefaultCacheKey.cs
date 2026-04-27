using AlbyOnContainers.Kernel.Caching.Abstractions;

namespace AlbyOnContainers.Kernel.Caching.Keys;

public record DefaultCacheKey<T> : ICacheKey
{
    private static readonly string EntityPrefix = typeof(T).Name.ToLowerInvariant();
    
    public string Value { get; }

    public DefaultCacheKey() => Value = $"{EntityPrefix}:all";

    public DefaultCacheKey(string identifier) => Value = $"{EntityPrefix}:{identifier.ToLowerInvariant()}";

    public DefaultCacheKey(Guid identifier) => Value = $"{EntityPrefix}:{identifier}";

    public static implicit operator string(DefaultCacheKey<T> key) => key.Value;
}