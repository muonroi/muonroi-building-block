namespace Muonroi.AuthZ.HotReload;

/// <summary>
/// Configuration for the authorization rule hot-reload client.
/// </summary>
public sealed class AuthRuleHotReloadOptions
{
    /// <summary>
    /// Base Control Plane URL or a full auth-rule hub URL.
    /// </summary>
    public string? ControlPlaneUrl { get; set; }

    /// <summary>
    /// Optional tenant group to subscribe to after connecting.
    /// </summary>
    public string? TenantId { get; set; }

    /// <summary>
    /// Optional bearer token factory used when the hub requires authentication.
    /// </summary>
    public Func<Task<string?>>? AccessTokenFactory { get; set; }

    /// <summary>
    /// Delay before retrying a failed initial connection.
    /// </summary>
    public TimeSpan ReconnectDelay { get; set; } = TimeSpan.FromSeconds(10);
}
