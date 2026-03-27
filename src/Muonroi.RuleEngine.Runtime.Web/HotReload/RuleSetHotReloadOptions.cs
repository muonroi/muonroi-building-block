namespace Muonroi.RuleEngine.Runtime.Web.HotReload;

/// <summary>
/// Configuration for the ruleset hot-reload client.
/// </summary>
public sealed class RuleSetHotReloadOptions
{
    /// <summary>
    /// Base Control Plane URL or a full ruleset-changes hub URL.
    /// </summary>
    public string? ControlPlaneUrl { get; set; }

    /// <summary>
    /// Subscribe to a single tenant group. Use for single-tenant consumers only.
    /// For multi-tenant consumers, set <see cref="SubscribeAllTenants"/> to true instead.
    /// When both are set, SubscribeAllTenants takes precedence.
    /// </summary>
    public string? TenantId { get; set; }

    /// <summary>
    /// When true, subscribes to the global all-tenants group to receive events for ALL tenants.
    /// Use for multi-tenant consumer apps that serve multiple tenants.
    /// The handler receives <see cref="Rules.RuleSetChangeEvent"/> with TenantId per event,
    /// so the consumer can invalidate the correct tenant's cache.
    /// </summary>
    public bool SubscribeAllTenants { get; set; }

    /// <summary>
    /// Optional bearer token factory used when the hub requires authentication.
    /// </summary>
    public Func<Task<string?>>? AccessTokenFactory { get; set; }

    /// <summary>
    /// Delay before retrying a failed initial connection.
    /// </summary>
    public TimeSpan ReconnectDelay { get; set; } = TimeSpan.FromSeconds(10);
}
