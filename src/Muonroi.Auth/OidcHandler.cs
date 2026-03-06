using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Muonroi.Auth;

public static class OidcHandler
{
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
