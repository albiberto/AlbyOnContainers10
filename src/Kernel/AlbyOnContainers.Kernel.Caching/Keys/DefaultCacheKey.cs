using AlbyOnContainers.Kernel.Caching.Abstractions;

namespace AlbyOnContainers.Kernel.Caching.Keys;

/// <summary>
///     Strongly-typed cache key generator based on the generic type.
///     Computes the entity prefix only once per type at runtime for peak performance.
/// </summary>
/// <typeparam name="T">The type of the entity or DTO being cached.</typeparam>
public record DefaultCacheKey<T>(string? Identifier = null) : ICacheKey
{
    private static readonly string EntityPrefix = typeof(T).Name.Replace("Dto", string.Empty).ToLowerInvariant();

    public string Value => string.IsNullOrWhiteSpace(Identifier) 
        ? $"{EntityPrefix}:all" 
        : $"{EntityPrefix}:{Identifier.ToLowerInvariant()}";

    public override string ToString() => Value;
}