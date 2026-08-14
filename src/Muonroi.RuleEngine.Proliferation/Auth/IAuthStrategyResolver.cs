namespace Muonroi.RuleEngine.Proliferation.Auth;

/// <summary>
/// Resolves authentication credentials for external project execution requests.
/// Dispatches to the appropriate auth strategy based on ExternalProjectConfig.AuthStrategy.
/// </summary>
public interface IAuthStrategyResolver
{
    /// <summary>
    /// Resolves authentication for the given config and returns an AuthResult
    /// containing resolved headers and/or a custom HttpClient (for mTLS).
    /// </summary>
    Task<AuthResult> ResolveAsync(ExternalProjectConfig config, CancellationToken ct);
}
