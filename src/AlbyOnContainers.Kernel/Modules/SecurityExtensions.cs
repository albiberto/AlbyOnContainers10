using Microsoft.Extensions.DependencyInjection;
using AlbyOnContainers.Kernel.Security;
using System;

namespace AlbyOnContainers.Kernel.Modules;

public static class SecurityExtensions
{
    /// <summary>
    /// Configures Keycloak Authentication as the security provider for the Kernel.
    /// This method enforces a fail-fast mechanism if critical configuration is missing.
    /// </summary>
    /// <param name="builder">The kernel builder.</param>
    /// <returns>The kernel builder for chaining.</returns>
    public static IAlbyKernelBuilder WithSecurity(this IAlbyKernelBuilder builder)
    {
        var keycloakSection = builder.Configuration.GetSection("Keycloak");

        // Enterprise Best Practice: Fail-Fast if critical infrastructure configuration is missing.
        var authority = keycloakSection["Authority"] ?? throw new InvalidOperationException("Keycloak Authority configuration is missing. Security cannot be established.");
        var clientId = keycloakSection["ClientId"] ?? throw new InvalidOperationException("Keycloak ClientId configuration is missing. Security cannot be established.");

        // Calls the shared security extension which sets up OpenID Connect
        builder.Services.AddKeycloakAuthentication(builder.Configuration);

        return builder;
    }
}
