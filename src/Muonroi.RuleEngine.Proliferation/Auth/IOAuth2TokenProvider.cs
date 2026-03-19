using Muonroi.RuleEngine.Proliferation.Models;

namespace Muonroi.RuleEngine.Proliferation.Auth;

/// <summary>
/// Provides OAuth2 access tokens using the client credentials flow.
/// Implementations should cache tokens and refresh proactively before expiry.
/// </summary>
public interface IOAuth2TokenProvider
{
    /// <summary>
    /// Returns a valid access token for the given OAuth2 config.
    /// Fetches a new token if none is cached or the cached token is near expiry.
    /// </summary>
    Task<string> GetAccessTokenAsync(OAuth2Config config, CancellationToken ct);

    /// <summary>
    /// Invalidates the cached token for the given config.
    /// Call this after receiving a 401 to force a fresh token on the next request.
    /// </summary>
    void InvalidateToken(OAuth2Config config);
}
