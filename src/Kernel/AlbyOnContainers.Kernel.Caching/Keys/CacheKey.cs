namespace AlbyOnContainers.Kernel.Caching.Keys;

public readonly record struct CacheKey(string Value)
{
    public static CacheKey Type<T>(string? id = null)
    {
        var typeName = typeof(T).Name;
        return string.IsNullOrWhiteSpace(id)
            ? new CacheKey(typeName)
            : new CacheKey($"{typeName}:{id}");
    }

    public static CacheKey User(string usernameOrEmail)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(usernameOrEmail);

        return new CacheKey($"User:{usernameOrEmail}");
    }

    public static CacheKey Custom(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return new CacheKey(key);
    }

    public CacheKey WithUser(string usernameOrEmail)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(usernameOrEmail);

        return new CacheKey($"{Value}:User:{usernameOrEmail}");
    }

    public CacheKey WithCustom(string custom)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(custom);

        return new CacheKey($"{Value}:{custom}");
    }

    public override string ToString() => Value;

    public static implicit operator string(CacheKey key) => key.Value;
}
