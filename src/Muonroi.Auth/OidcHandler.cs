using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Muonroi.Auth;

/// <summary>
/// Provides extension methods for OpenID Connect (OIDC) authentication.
/// </summary>
public static class OidcHandler
{
    /// <summary>
    /// Adds OpenID Connect login authentication to the service collection.
    /// </summary>
    /// <param name="services">The service collection to add authentication to.</param>
    /// <param name="configuration">The configuration to bind OIDC settings from.</param>
    /// <param name="scheme">The authentication scheme name (default is "oidc").</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddOidcLogin(this IServiceCollection services, IConfiguration configuration,
        string scheme = "oidc")
    {
        MOidcConfig oidc = new();
        configuration.GetSection(MOidcConfig.SectionName).Bind(oidc);

        _ = services.AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = scheme;
            })
            .AddCookie()
            .AddOpenIdConnect(scheme, options =>
            {
                options.Authority = oidc.Authority;
                options.ClientId = oidc.ClientId;
                options.ClientSecret = oidc.ClientSecret;
                options.CallbackPath = oidc.CallbackPath;
                options.ResponseType = OpenIdConnectResponseType.Code;
                options.SaveTokens = true;
                foreach (string scope in oidc.Scopes)
                {
                    options.Scope.Add(scope);
                }
            });

        return services;
    }
}
