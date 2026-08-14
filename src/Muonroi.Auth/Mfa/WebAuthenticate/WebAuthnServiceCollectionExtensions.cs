namespace Muonroi.Auth.Mfa.WebAuthenticate;

/// <summary>
/// Service registration helpers for WebAuthn.
/// </summary>
public static class WebAuthnServiceCollectionExtensions
{
    /// <summary>
    /// Registers WebAuthn services and configuration.
    /// </summary>
    public static IServiceCollection AddWebAuthn(this IServiceCollection services, IConfiguration configuration)
    {
        MGuard.NotNull(configuration);

        HashSet<string> origins =
            configuration.GetSection("WebAuthn:Origins").Get<HashSet<string>>() ??
            [];

        services.AddFido2(options =>
        {
            options.ServerDomain = configuration["WebAuthn:ServerDomain"] ?? string.Empty;
            options.ServerName = configuration["WebAuthn:ServerName"] ?? "Muonroi";
            options.Origins = origins;
            options.TimestampDriftTolerance = 300_000;
        });

        services.AddScoped<WebAuthenticateService>();
        return services;
    }
}
