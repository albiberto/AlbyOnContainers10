using AlbyOnContainers.Kernel.Security.Abstractions;
using AlbyOnContainers.Kernel.Security.Options;
using AlbyOnContainers.Kernel.Security.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace AlbyOnContainers.Kernel.Security;

public static class SecurityKernelExtensions
{
    extension(IKernelBuilder builder)
    {
        public IKernelBuilder WithSecurity(string? section = null)
        {
            builder.BindOptions(section);
            builder.ConfigureSecurity();
            return builder;
        }

        public IKernelBuilder WithSecurity(Action<KeycloakOptions> configureOptions)
        {
            builder.ConfigureOptions(configureOptions);
            builder.ConfigureSecurity();
            return builder;
        }
        
        private void BindOptions(string? section)
        {
            builder.Host.Services
                .AddOptions<KeycloakOptions>()
                .BindConfiguration(section ?? KeycloakOptions.Section)
                .ValidateDataAnnotations()
                .ValidateOnStart();
        }

        private void ConfigureOptions(Action<KeycloakOptions> configure)
        {
            builder.Host.Services
                .AddOptions<KeycloakOptions>()
                .Configure(configure)
                .ValidateDataAnnotations()
                .ValidateOnStart();
        }

        private void ConfigureSecurity()
        {
            var services = builder.Host.Services;

            services.AddHttpContextAccessor();

            services.AddScoped<ICurrentUserService>(sp =>
            {
                var opts = sp.GetRequiredService<IOptions<KeycloakOptions>>().Value;
                var logger = sp.GetRequiredService<ILogger<CurrentUserService>>();

                if (!opts.EnableStub) return new CurrentUserService(sp.GetRequiredService<IHttpContextAccessor>());
                
                logger.LogWarning("SECURITY ALERT: Running in STUB mode. Authentication claims are completely mocked. DO NOT use this in Production.");
                return new StubCurrentUserService();

            });

            services.AddAuthentication(authOptions =>
                {
                    authOptions.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                    authOptions.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
                })
                .AddCookie()
                .AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, _ => { });

            services.AddOptions<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme)
                .Configure<IOptions<KeycloakOptions>>((cookie, keycloakAccessor) =>
                {
                    var cfg = keycloakAccessor.Value;
                    cookie.ExpireTimeSpan = cfg.CookieExpiration;
                    cookie.SlidingExpiration = true;
                });

            services.AddOptions<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme)
                .Configure<IOptions<KeycloakOptions>>((oidc, keycloakAccessor) =>
                {
                    var cfg = keycloakAccessor.Value;

                    // Bail out completely if we are running the Stub. 
                    // This prevents OpenIdConnectHandler from trying to contact an empty Authority.
                    if (cfg.EnableStub) return;
        
                    oidc.Authority = cfg.Authority;
                    oidc.ClientId = cfg.ClientId;
                    oidc.ClientSecret = cfg.ClientSecret;
                    oidc.ResponseType = OpenIdConnectResponseType.Code;
                    oidc.SaveTokens = cfg.SaveTokens;
                    oidc.GetClaimsFromUserInfoEndpoint = true;
                    oidc.MapInboundClaims = false;
                    oidc.RequireHttpsMetadata = cfg.RequireHttpsMetadata;

                    oidc.Scope.Clear();
                    foreach (var scope in cfg.Scopes)
                    {
                        oidc.Scope.Add(scope);
                    }

                    foreach (var mapping in cfg.ClaimMappings)
                    {
                        oidc.ClaimActions.MapJsonKey(mapping.Key, mapping.Value);
                    }

                    oidc.TokenValidationParameters.NameClaimType = cfg.NameClaimType;
                    oidc.TokenValidationParameters.RoleClaimType = cfg.RoleClaimType;
                });

            services.AddAuthorization();
        }
    }
}