namespace AlbyOnContainers.Kernel.Messaging.Contracts;

public record ContractBase(long Version = 0)
{
    public DateTimeOffset OccurredOn = DateTimeOffset.UtcNow;
}