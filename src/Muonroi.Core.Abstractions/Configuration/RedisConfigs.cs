namespace Muonroi.Core.Abstractions.Configuration;

public class RedisConfigs
{
    public const string DefaultSectionName = "RedisConfigs";
    public string SectionName { get; set; } = DefaultSectionName;
    public string Host { get; set; } = string.Empty;
    public string Port { get; set; } = string.Empty;
    public string InstanceName { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool AllowAdmin { get; set; }
    public bool Enable { get; set; }
    public bool AllMethodsEnableCache { get; set; }
    public string KeyPrefix { get; set; } = string.Empty;
    public int Expire { get; set; }
    public bool AbortOnConnectFail { get; set; }
}
