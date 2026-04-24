using System;
using AlbyOnContainers.Kernel.Abstraction;
using AlbyOnContainers.Kernel.Security.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace AlbyOnContainers.Kernel.Security;

public static class SecurityKernelExtensions
{
    public static IKernelBuilder WithKeycloakAuthentication(this IKernelBuilder builder, string sectionName = "Keycloak")
    {
        var section = builder.Host.Configuration.GetSection(sectionName);
        if (!section.Exists() || string.IsNullOrWhiteSpace(section["Authority"]))
        {
            throw new InvalidOperationException($"Fail-Fast: Configuration section '{sectionName}' is missing or 'Authority' is not set. Security cannot be established.");
        }

        builder.Host.Services.AddOptions<KeycloakOptions>()
            .Bind(section)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.AddInternalKeycloakAuth();
        return builder;
    }

    public static IKernelBuilder WithKeycloakAuthentication(this IKernelBuilder builder, Action<KeycloakOptions> configureOptions)
    {
        builder.Host.Services.AddOptions<KeycloakOptions>()
            .Configure(configureOptions)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.AddInternalKeycloakAuth();
        return builder;
    }

    private static IKernelBuilder AddInternalKeycloakAuth(this IKernelBuilder builder)
    {
        // Bind the options early to know the SchemeName
        var keycloakOptions = builder.Host.Configuration.GetSection("Keycloak").Get<KeycloakOptions>() ?? new KeycloakOptions();

        builder.Host.Services.AddAuthentication(options =>
        {
            options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = keycloakOptions.SchemeName;
        })
        .AddCookie()
        .AddOpenIdConnect(keycloakOptions.SchemeName, options =>
        {
            // The options are configured via IConfigureNamedOptions
        });

        builder.Host.Services.AddOptions<OpenIdConnectOptions>(keycloakOptions.SchemeName)
            .Configure<IOptions<KeycloakOptions>>((options, keycloakOptionsAccessor) =>
            {
                var cfg = keycloakOptionsAccessor.Value;
                options.Authority = cfg.Authority;
                options.ClientId = cfg.ClientId;
                options.ClientSecret = cfg.ClientSecret;
                options.ResponseType = OpenIdConnectResponseType.Code;
                options.SaveTokens = true;
                options.GetClaimsFromUserInfoEndpoint = true;
                options.MapInboundClaims = false;
                options.RequireHttpsMetadata = cfg.RequireHttpsMetadata;

                options.Scope.Clear();
                options.Scope.Add("openid");
                options.Scope.Add("profile");
                options.Scope.Add("email");

                foreach (var mapping in cfg.ClaimMappings)
                {
                    options.ClaimActions.MapJsonKey(mapping.Key, mapping.Value);
                }

                options.TokenValidationParameters.NameClaimType = "preferred_username";
                options.TokenValidationParameters.RoleClaimType = "roles";
            });

        builder.Host.Services.AddAuthorization();
        return builder;
    }
}
