namespace AlbyOnContainers.Kernel.Options;

public abstract record KernelOptions<T>
{
    private const string Suffix = "Options";

    public static readonly string Section = typeof(T).Name.EndsWith(Suffix, StringComparison.Ordinal)
        ? typeof(T).Name[..^Suffix.Length]
        : typeof(T).Name;
}