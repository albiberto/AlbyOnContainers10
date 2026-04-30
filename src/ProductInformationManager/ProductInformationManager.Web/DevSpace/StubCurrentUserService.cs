using AlbyOnContainers.Kernel.Security.Abstractions;

namespace ProductInformationManager.Web.DevSpace;

public class StubCurrentUserService : ICurrentUserService
{
    public string? UserId => "Alberto";
    public string? UserName => "Alberto";
    public string? Email => "alberto@viezzi.it";
    public bool IsAuthenticated => true;

    public IReadOnlyCollection<string> Roles { get; } = ["Administrator"];

    public string? GetClaim(string claimType) => null;

    public bool IsInRole(string role) => Roles.Contains(role);
}