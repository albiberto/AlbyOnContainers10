namespace AlbyOnContainers.Kernel.Security.Services;

using Abstractions;

public sealed class StubCurrentUserService : ICurrentUserService
{
    public string UserId => "00000000-0000-0000-0000-000000000001";
    public string UserName => "dev_admin";
    public string Email => "admin@albyoncontainers.local";
    public bool IsAuthenticated => true;

    public IReadOnlyCollection<string> Roles { get; } = ["Administrator"];

    // Mocks Keycloak response using standard JWT keys
    public string? GetClaim(string claimType) =>
        claimType switch
        {
            "roles" => "Administrator", // Standardized to OIDC naming
            "preferred_username" => UserName,
            "sub" => UserId,
            "email" => Email,
            "given_name" => "Developer",
            "family_name" => "Admin",
            _ => null
        };

    public bool IsInRole(string role) => Roles.Contains(role);
}