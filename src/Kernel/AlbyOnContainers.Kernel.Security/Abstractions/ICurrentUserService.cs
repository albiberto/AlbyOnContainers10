namespace AlbyOnContainers.Kernel.Security.Abstractions;

public interface ICurrentUserService
{
    string? UserId { get; }
    string? UserName { get; }
    string? Email { get; }
    bool IsAuthenticated { get; }

    string? GetClaim(string claimType);
}