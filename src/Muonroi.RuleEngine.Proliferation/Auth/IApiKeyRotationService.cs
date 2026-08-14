namespace Muonroi.RuleEngine.Proliferation.Auth;

/// <summary>
/// Checks whether an API key needs rotation and performs the rotation if necessary.
/// Rotation is triggered when the key expiry is within 1 hour.
/// On rotation failure, the existing key is preserved (graceful degradation).
/// </summary>
public interface IApiKeyRotationService
{
    /// <summary>
    /// Returns the current execution headers, rotating the API key if expiry is within 1 hour.
    /// If rotation fails, returns the existing headers unchanged.
    /// </summary>
    Task<IReadOnlyDictionary<string, string>> CheckAndRotateAsync(ExternalProjectConfig config, CancellationToken ct);
}
