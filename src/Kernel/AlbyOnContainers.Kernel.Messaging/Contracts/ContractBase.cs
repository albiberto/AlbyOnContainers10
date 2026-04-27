namespace AlbyOnContainers.Kernel.Messaging.Contracts;

public abstract record ContractBase
{
    public required DateTimeOffset OccurredOn { get; init; }
}