using AlbyOnContainers.Kernel.Security.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace AlbyOnContainers.Kernel.Security;

public static class KeycloakAuthenticationExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddAlbyKeycloakAuthentication(IConfiguration configuration, string sectionName = "Keycloak")
        {
            services.AddOptions<KeycloakOptions>().Bind(configuration.GetSection(sectionName)).ValidateDataAnnotations().ValidateOnStart();

            return services.AddInternalKeycloakAuth();
        }

        public IServiceCollection AddAlbyKeycloakAuthentication(Action<KeycloakOptions> configureOptions)
        {
            services.AddOptions<KeycloakOptions>().Configure(configureOptions).ValidateDataAnnotations().ValidateOnStart();

            return services.AddInternalKeycloakAuth();
        }

        private IServiceCollection AddInternalKeycloakAuth()
        {
            using var serviceProvider = services.BuildServiceProvider();
            var keycloakOptions = serviceProvider.GetRequiredService<IOptions<KeycloakOptions>>().Value;

            services.AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = keycloakOptions.SchemeName;
            }).AddCookie().AddOpenIdConnect(keycloakOptions.SchemeName, options =>
            {
                options.Authority = keycloakOptions.Authority;
                options.ClientId = keycloakOptions.ClientId;
                options.ClientSecret = keycloakOptions.ClientSecret;
                options.ResponseType = OpenIdConnectResponseType.Code;
                options.SaveTokens = true;
                options.GetClaimsFromUserInfoEndpoint = true;
                options.MapInboundClaims = false;
                options.RequireHttpsMetadata = keycloakOptions.RequireHttpsMetadata;

                options.Scope.Clear();
                options.Scope.Add("openid");
                options.Scope.Add("profile");
                options.Scope.Add("email");

                foreach (var mapping in keycloakOptions.ClaimMappings) options.ClaimActions.MapJsonKey(mapping.Key, mapping.Value);

                options.TokenValidationParameters.NameClaimType = "preferred_username";
                options.TokenValidationParameters.RoleClaimType = "roles";
            });

            services.AddAuthorization();
            return services;
        }
    }
}