using AlbyOnContainers.Shared.Application.Abstract;

namespace ProductInformationManager.Web.DevSpace;

public class StubCurrentUserService : ICurrentUserService
{
    public string? UserId => "Alberto"; 
    public string? UserName => "Alberto";
    public bool IsAuthenticated => true;
}