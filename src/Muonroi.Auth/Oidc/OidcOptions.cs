namespace Muonroi.Auth.Oidc;

/// <summary>
/// Options used by the OIDC PKCE client.
/// </summary>
public class OidcOptions
{
    /// <summary>
    /// Base authority of the OpenID Connect provider.
    /// </summary>
    public string Authority { get; set; } = string.Empty;

    /// <summary>
    /// Client identifier registered with the provider.
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Redirect URI that must exactly match the one registered.
    /// </summary>
    public string RedirectUri { get; set; } = string.Empty;

    /// <summary>
    /// Scopes requested during authentication.
    /// </summary>
    public string[] Scopes { get; set; } = [];

    /// <summary>
    /// Gets the authorization endpoint based on the authority.
    /// </summary>
    public string AuthorizationEndpoint => $"{Authority.TrimEnd('/')}/authorize";

    /// <summary>
    /// Gets the token endpoint based on the authority.
    /// </summary>
    public string TokenEndpoint => $"{Authority.TrimEnd('/')}/token";
}
