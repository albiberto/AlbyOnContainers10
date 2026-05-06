using System.ComponentModel.DataAnnotations;
using AlbyOnContainers.Kernel.Options;

namespace AlbyOnContainers.Kernel.Security.Options;

public sealed record KeycloakOptions : KernelOptions<KeycloakOptions>, IValidatableObject
{
    public bool EnableStub { get; set; } = false;

    public string Authority { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;

    public bool RequireHttpsMetadata { get; set; } = false;

    public bool SaveTokens { get; set; } = true;
    public TimeSpan CookieExpiration { get; set; } = TimeSpan.FromMinutes(60);

    public string NameClaimType { get; set; } = "preferred_username";
    public string RoleClaimType { get; set; } = "roles";

    public List<string> Scopes { get; set; } = ["openid", "profile", "email", "roles"];

    public Dictionary<string, string> ClaimMappings { get; set; } = new()
    {
        { "sub", "sub" },
        { "given_name", "given_name" },
        { "family_name", "family_name" },
        { "email", "email" },
        { "roles", "roles" },
        { "preferred_username", "preferred_username" }
    };

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (EnableStub) yield break;
        
        if (string.IsNullOrWhiteSpace(Authority))
            yield return new("Keycloak Authority is required when Stub is disabled.", [nameof(Authority)]);
        else if (!Uri.TryCreate(Authority, UriKind.Absolute, out _))
            yield return new("Keycloak Authority must be a valid URL.", [nameof(Authority)]);

        if (string.IsNullOrWhiteSpace(ClientId))
            yield return new("Keycloak ClientId is required when Stub is disabled.", [nameof(ClientId)]);

        if (string.IsNullOrWhiteSpace(ClientSecret))
            yield return new("Keycloak ClientSecret is required when Stub is disabled.", [nameof(ClientSecret)]);
    }
}