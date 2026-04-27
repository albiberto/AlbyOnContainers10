using System.Security.Claims;
using AlbyOnContainers.Kernel.Security.Abstractions;
using Microsoft.AspNetCore.Http;

namespace AlbyOnContainers.Kernel.Security.Services;

public sealed class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    private ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

    public string? UserId => User?.FindFirstValue(JwtClaims.Subject);
    public string? UserName => User?.FindFirstValue(JwtClaims.PreferredUsername) ?? User?.Identity?.Name;
    public string? Email => User?.FindFirstValue(JwtClaims.Email);
    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    public string? GetClaim(string claimType) => User?.FindFirstValue(claimType);
}