namespace Muonroi.Caching.Memory.MultiLevel;

/// <summary>
/// Cache layer selection.
/// </summary>
public enum MultiLevelCacheType
{
    /// <summary>
    /// In-memory cache only.
    /// </summary>
    Memory,

    /// <summary>
    /// Redis cache only.
    /// </summary>
    Redis,

    /// <summary>
    /// Combine memory and distributed cache.
    /// </summary>
    MultiLevel
}
