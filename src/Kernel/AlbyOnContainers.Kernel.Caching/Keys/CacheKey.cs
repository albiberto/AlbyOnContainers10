namespace AlbyOnContainers.Kernel.Caching.Keys;

public readonly record struct CacheKey(string Value)
{
    public static string For<TEntity>(string identifier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);

        return $"{typeof(TEntity).Name}:{identifier}";
    }

    public static string For<TEntity>(Guid id) => $"{typeof(TEntity).Name}:{id}";

    public override string ToString() => Value;

    public static implicit operator string(CacheKey key) => key.Value;
}
