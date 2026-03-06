namespace Muonroi.Bff;

/// <summary>
/// Service registration helpers for BFF authentication and CSRF protection.
/// Tokens are handled server-side and the SPA communicates via secure cookies.
/// </summary>
public static class BffAuthenticationExtensions
{
    /// <summary>
    /// Configure cookie authentication and antiforgery services for a SPA/BFF setup.
    /// Cookies are marked Secure, HttpOnly and SameSite=Strict to prevent client-side access
    /// and cross-site posting. A server-side <see cref="ITokenStore"/> keeps refresh tokens away
    /// from the browser.
    /// </summary>
    public static void AddBffAuthentication(this IServiceCollection services, bool useRedisTokenStore = false)
    {
        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Strict;
            });

        services.AddAntiforgery(options =>
        {
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Strict;
        });

        if (useRedisTokenStore)
        {
            services.AddScoped<ITokenStore, RedisTokenStore>();
        }
        else
        {
            services.AddScoped<ITokenStore, InMemoryTokenStore>();
        }
    }
}
