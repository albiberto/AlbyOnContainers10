using AlbyOnContainers.Kernel.Security.Abstractions;

namespace ProductInformationManager.Web.DevSpace;

public class StubCurrentUserService : ICurrentUserService
{
    public string? UserId => "Alberto"; 
    public string? UserName => "Alberto";
    public string? Email => "alberto@viezzi.it";
    public bool IsAuthenticated => true;

    public string? GetClaim(string claimType) => null;
}