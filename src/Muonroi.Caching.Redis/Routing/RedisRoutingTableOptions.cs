namespace Muonroi.Caching.Redis.Routing;

/// <summary>
/// Configures the Redis-backed routing table store.
/// </summary>
public sealed class RedisRoutingTableOptions
{
    /// <summary>
    /// Gets or sets the local in-process cache time-to-live.
    /// </summary>
    public TimeSpan LocalCacheTtl { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Gets or sets the key prefix used for Redis hashes.
    /// </summary>
    public string KeyPrefix { get; set; } = "mrt";

    /// <summary>
    /// Gets or sets the pub/sub channel prefix used for invalidation.
    /// </summary>
    public string ChannelName { get; set; } = "routing-table-changed";
}
