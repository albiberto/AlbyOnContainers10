namespace AlbyOnContainers.Kernel.Messaging.Filters;

/// <summary>
///     Mutable registry of open-generic consume filter types added by application code via
///     <c>AddMessagingFilter</c>. The kernel reads it at MassTransit configuration time and
///     applies each filter to both the in-process mediator pipeline and the out-of-process
///     bus pipeline, after the kernel-mandated filters (GlobalException + Validation).
/// </summary>
public sealed class MessagingFilterRegistry
{
    private readonly List<Type> _filters = [];

    public IReadOnlyList<Type> Filters => _filters;

    public void Add(Type openGenericFilter) => _filters.Add(openGenericFilter);
}
