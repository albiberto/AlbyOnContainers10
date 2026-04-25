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
    public static IKernelBuilder WithSecurity(this IKernelBuilder builder, string sectionName = "Keycloak")
    {
        builder.Host.Services.AddOptions<KeycloakOptions>()
            .BindConfiguration(sectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.AddInternalKeycloakAuth(typeof(SecurityKernelExtensions).Assembly);
        return builder;
    }

    public static IKernelBuilder WithSecurity(this IKernelBuilder builder, Action<KeycloakOptions> configureOptions)
    {
        builder.Host.Services.AddOptions<KeycloakOptions>()
            .Configure(configureOptions)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.AddInternalKeycloakAuth(typeof(SecurityKernelExtensions).Assembly);
        return builder;
    }

    public static IKernelBuilder WithSecurity<TMarker>(this IKernelBuilder builder, string sectionName = "Keycloak")
    {
        builder.Host.Services.AddOptions<KeycloakOptions>()
            .BindConfiguration(sectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.AddInternalKeycloakAuth(typeof(TMarker).Assembly);
        return builder;
    }

    private static IKernelBuilder AddInternalKeycloakAuth(this IKernelBuilder builder, System.Reflection.Assembly scanAssembly)
    {
        builder.Host.Services.AddAuthentication(options =>
        {
            options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
        })
        .AddCookie()
        .AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
        {
            // Configure via IConfigureNamedOptions
        });

        builder.Host.Services.AddOptions<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme)
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
