using AlbyOnContainers.Kernel.Security.Abstractions;

namespace AlbyOnContainers.Kernel.Persistence.IntegrationTests.Fakes;

public class StubCurrentUserService : ICurrentUserService
{
    public string? UserId { get; set; } = "TestUser";
    public string TenantId { get; set; } = "TestTenant";
    public string? UserName { get; set; }
    public string? Email { get; set; }
    public bool IsAuthenticated { get; set; } = true;
    public IReadOnlyCollection<string> Roles { get; set; } = Array.Empty<string>();

    public string? GetClaim(string claimType) => null;

    public bool IsInRole(string role) => Roles.Contains(role);
}
