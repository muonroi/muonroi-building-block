namespace Muonroi.Caching.Memory.MultiLevel;

/// <summary>
/// Configuration settings for multi-level caching.
/// </summary>
public class CacheConfigs
{
    /// <summary>
    /// Default configuration section name.
    /// </summary>
    public const string DefaultSectionName = "CacheConfigs";

    /// <summary>
    /// Configuration section name to bind from.
    /// </summary>
    public string SectionName { get; set; } = DefaultSectionName;

    /// <summary>
    /// Cache layer selection.
    /// </summary>
    public MultiLevelCacheType CacheType { get; set; } = MultiLevelCacheType.Memory;

    /// <summary>
    /// Optional key namespace prefix.
    /// </summary>
    public string KeyNamespace { get; set; } = string.Empty;

    /// <summary>
    /// Enables cache stampede protection.
    /// </summary>
    public bool EnableStampedeProtection { get; set; } = true;

    /// <summary>
    /// Default absolute expiration in minutes.
    /// </summary>
    public int DefaultAbsoluteExpirationInMinutes { get; set; } = 1440;

    /// <summary>
    /// TTL jitter percentage for expiration randomization.
    /// </summary>
    public int TtlJitterPercent { get; set; }
}
