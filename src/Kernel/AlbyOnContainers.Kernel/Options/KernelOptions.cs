namespace AlbyOnContainers.Kernel.Options;

/// <summary>
/// Base class for every options POCO bound from configuration via the kernel fluent API.
/// </summary>
/// <typeparam name="T">The concrete options type, used to derive the configuration section name.</typeparam>
/// <remarks>
/// <para>
/// The default section name is the type name with the trailing "Options" suffix removed,
/// so <c>CachingOptions</c> binds <c>"Caching"</c>, <c>MessagingOptions</c> binds <c>"Messaging"</c>, etc.
/// This matches the convention used across the wider .NET ecosystem.
/// </para>
/// <para>
/// Each kernel module's <c>WithXxx(...)</c> entry point also accepts an explicit
/// <c>section</c> parameter that overrides this default.
/// </para>
/// </remarks>
public abstract class KernelOptions<T>
{
    private const string OptionsSuffix = "Options";

    /// <summary>
    /// Default configuration section name for <typeparamref name="T"/>.
    /// </summary>
    public static readonly string Section =
        typeof(T).Name.EndsWith(OptionsSuffix, StringComparison.Ordinal)
            ? typeof(T).Name[..^OptionsSuffix.Length]
            : typeof(T).Name;
}