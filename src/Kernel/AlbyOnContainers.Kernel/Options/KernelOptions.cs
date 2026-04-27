namespace AlbyOnContainers.Kernel.Options;

public abstract class KernelOptions<T>
{
    public static readonly string Section = typeof(T).Name;
}