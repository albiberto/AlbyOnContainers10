using AlbyOnContainers.Kernel.Domain.SeedWork;

namespace AlbyOnContainers.Kernel.Persistence.IntegrationTests.Fakes;

public class FakeAuditableEntity : AuditableEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
