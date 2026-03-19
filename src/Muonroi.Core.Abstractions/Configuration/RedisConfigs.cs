namespace Muonroi.Core.Abstractions.Configuration;

/// <summary>
/// Configuration options for Redis.
/// </summary>
public class RedisConfigs
{
    /// <summary>
    /// The default configuration section name.
    /// </summary>
    public const string DefaultSectionName = "RedisConfigs";

    /// <summary>
    /// Gets or sets the configuration section name.
    /// </summary>
    public string SectionName { get; set; } = DefaultSectionName;

    /// <summary>
    /// Gets or sets the Redis host address.
    /// </summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Redis port.
    /// </summary>
    public string Port { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Redis instance name.
    /// </summary>
    public string InstanceName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Redis client name.
    /// </summary>
    public string ClientName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Redis password.
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether administrative operations are allowed.
    /// </summary>
    public bool AllowAdmin { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether Redis is enabled.
    /// </summary>
    public bool Enable { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether all methods should enable cache.
    /// </summary>
    public bool AllMethodsEnableCache { get; set; }

    /// <summary>
    /// Gets or sets the prefix for Redis keys.
    /// </summary>
    public string KeyPrefix { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the expiration time in seconds.
    /// </summary>
    public int Expire { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to abort on connection failure.
    /// </summary>
    public bool AbortOnConnectFail { get; set; }
}
