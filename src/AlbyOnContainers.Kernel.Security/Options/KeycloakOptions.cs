using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace AlbyOnContainers.Kernel.Security.Options;

public sealed class KeycloakOptions
{
    [Required(ErrorMessage = "L'Authority di Keycloak è obbligatoria."), Url(ErrorMessage = "L'Authority deve essere un URL valido.")]
    public string Authority { get; set; } = string.Empty;

    [Required(ErrorMessage = "Il ClientId di Keycloak è obbligatorio.")]
    public string ClientId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Il ClientSecret di Keycloak è obbligatorio.")]
    public string ClientSecret { get; set; } = "secret";

    public bool RequireHttpsMetadata { get; set; } = false;

    public string SchemeName { get; set; } = "OpenIdConnect";

    public Dictionary<string, string> ClaimMappings { get; set; } = new()
    {
        { ClaimTypes.NameIdentifier, "sub" },
        { ClaimTypes.GivenName, "given_name" },
        { ClaimTypes.Surname, "family_name" },
        { ClaimTypes.Email, "email" }
    };
}