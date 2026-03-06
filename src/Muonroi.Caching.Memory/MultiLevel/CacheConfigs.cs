namespace Muonroi.Caching.Memory.MultiLevel;

public class CacheConfigs
{
    public const string DefaultSectionName = "CacheConfigs";
    public string SectionName { get; set; } = DefaultSectionName;
    public MultiLevelCacheType CacheType { get; set; } = MultiLevelCacheType.Memory;
    public string KeyNamespace { get; set; } = string.Empty;
    public bool EnableStampedeProtection { get; set; } = true;
    public int DefaultAbsoluteExpirationInMinutes { get; set; } = 1440;
    public int TtlJitterPercent { get; set; }
}
