namespace AlbyOnContainers.Kernel.Caching.Cache;

public readonly record struct Key(string Value)
{
    public static Key Type<T>(string? id = null)
    {
        var typeName = typeof(T).Name;
        return string.IsNullOrWhiteSpace(id)
            ? new(typeName)
            : new Key($"{typeName}:{id}");
    }

    public static Key User(string usernameOrEmail)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(usernameOrEmail);

        return new($"User:{usernameOrEmail}");
    }

    public static Key Custom(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return new(key);
    }

    public Key WithUser(string usernameOrEmail)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(usernameOrEmail);

        return new($"{Value}:User:{usernameOrEmail}");
    }

    public Key WithCustom(string custom)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(custom);

        return new($"{Value}:{custom}");
    }

    public override string ToString() => Value;

    public static implicit operator string(Key key) => key.Value;
}
