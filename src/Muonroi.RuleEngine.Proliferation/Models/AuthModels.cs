namespace Muonroi.RuleEngine.Proliferation.Models;

/// <summary>
/// Result from IAuthStrategyResolver.ResolveAsync — contains resolved auth headers and optional custom HttpClient.
/// </summary>
public sealed record AuthResult
{
    /// <summary>
    /// Resolved authentication headers to merge into the request.
    /// For StaticHeaders: rotated API key headers (or original if no rotation needed).
    /// For OAuth2: { "Authorization": "Bearer {token}" }.
    /// For MutualTls: empty (auth is at TLS layer via CustomHttpClient).
    /// For None: empty.
    /// </summary>
    public required IReadOnlyDictionary<string, string> Headers { get; init; }

    /// <summary>
    /// Custom HttpClient pre-configured with mTLS client certificate.
    /// Non-null only for MutualTls strategy. Null for all other strategies.
    /// </summary>
    public HttpClient? CustomHttpClient { get; init; }
}
