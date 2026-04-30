namespace AlbyOnContainers.Kernel.Security.Abstractions;

public interface ICurrentUserService
{
    string? UserId { get; }
    string? UserName { get; }
    string? Email { get; }
    bool IsAuthenticated { get; }

    /// <summary>
    /// Roles assigned to the current user, or an empty collection if anonymous.
    /// </summary>
    IReadOnlyCollection<string> Roles { get; }

    string? GetClaim(string claimType);

    /// <summary>
    /// Returns true if the current user has the requested role.
    /// </summary>
    bool IsInRole(string role);
}