using AlbyOnContainers.Kernel.Messaging.Contracts;

namespace ProductInformationManager.Contracts;

public record CategoryDeletedEvent(Guid Id) : ContractBase;