namespace AlbyOnContainers.Kernel.Messaging.Contracts;

public abstract record ContractBase(long Version = 0)
{
    public DateTimeOffset OccurredOn = DateTimeOffset.UtcNow;
}