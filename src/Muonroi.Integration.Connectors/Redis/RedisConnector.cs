using System.Text.Json;
using Muonroi.Core.Abstractions.Exceptions;
using Muonroi.Integration.Abstractions;
using StackExchange.Redis;

namespace Muonroi.Integration.Connectors.Redis;

/// <summary>
/// Redis connector supporting GET, SET, and PUB operations.
/// </summary>
/// <remarks>
/// Creates a Redis connector.
/// </remarks>
/// <param name="redis">Optional Redis connection multiplexer.</param>
public sealed class RedisConnector(IConnectionMultiplexer? redis = null) : IServiceTaskConnector
{
    private readonly IConnectionMultiplexer? _redis = redis;

    /// <summary>
    /// Connector metadata describing capabilities and configuration.
    /// </summary>
    public ConnectorMetadata Metadata => new()
    {
        Type = "redis",
        DisplayName = "Redis",
        Category = "Database",
        IconSvg = "<path d=\"M12 2L2 7l10 5 10-5-10-5zM2 17l10 5 10-5M2 12l10 5 10-5\"/>",
        Description = "Perform GET, SET, or PUB operations on a Redis instance.",
        RequiresCredentials = false
    };

    /// <summary>
    /// Executes a Redis command based on the connector configuration.
    /// </summary>
    /// <param name="context">Connector execution context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The connector result.</returns>
    public async Task<ConnectorResult> ExecuteAsync(ConnectorContext context, CancellationToken ct)
    {
        if (_redis is null)
        {
            return ConnectorResult.Fail("Redis IConnectionMultiplexer not available.");
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();
        JsonElement root = context.Config.RootElement;

        string operation = root.GetProperty("operation").GetString()?.ToUpperInvariant()
            ?? throw new MInternalException("operation is required");
        string key = root.GetProperty("key").GetString()
            ?? throw new MInternalException("key is required");

        IDatabase db = _redis.GetDatabase();

        try
        {
            Dictionary<string, object?> output = new();
            switch (operation)
            {
                case "GET":
                    RedisValue val = await db.StringGetAsync(key);
                    output["redisValue"] = val.HasValue ? val.ToString() : null;
                    output["redisExists"] = val.HasValue;
                    break;

                case "SET":
                    string value = root.GetProperty("value").GetString() ?? "";
                    int? ttlSeconds = root.TryGetProperty("ttlSeconds", out var ttl) ? ttl.GetInt32() : null;
                    bool set = ttlSeconds.HasValue
                        ? await db.StringSetAsync(key, value, TimeSpan.FromSeconds(ttlSeconds.Value))
                        : await db.StringSetAsync(key, value);
                    output["redisSet"] = set;
                    break;

                case "PUB":
                case "PUBLISH":
                    string message = root.GetProperty("message").GetString() ?? "";
                    long subscribers = await db.PublishAsync(RedisChannel.Literal(key), message);
                    output["redisPublished"] = true;
                    output["redisSubscribers"] = subscribers;
                    break;

                default:
                    return ConnectorResult.Fail($"Unsupported Redis operation: {operation}");
            }

            sw.Stop();
            return ConnectorResult.Ok(output, duration: sw.Elapsed);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return ConnectorResult.Fail($"Redis error: {ex.Message}", duration: sw.Elapsed);
        }
    }

    /// <summary>
    /// Tests connectivity to Redis.
    /// </summary>
    /// <param name="context">Connector execution context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if the ping succeeds.</returns>
    public async Task<bool> TestConnectionAsync(ConnectorContext context, CancellationToken ct)
    {
        try
        {
            if (_redis is null) return false;
            IDatabase db = _redis.GetDatabase();
            await db.PingAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Returns the JSON schema used to configure this connector.
    /// </summary>
    /// <returns>Configuration schema.</returns>
    public JsonElement GetConfigSchema()
    {
        string schema = """
        {
            "type": "object",
            "properties": {
                "operation": { "type": "string", "enum": ["GET", "SET", "PUBLISH"], "description": "Redis operation" },
                "key": { "type": "string", "description": "Redis key or channel name" },
                "value": { "type": "string", "description": "Value for SET operations" },
                "message": { "type": "string", "description": "Message for PUBLISH operations" },
                "ttlSeconds": { "type": "integer", "description": "TTL in seconds for SET operations" }
            },
            "required": ["operation", "key"]
        }
        """;
        return JsonDocument.Parse(schema).RootElement.Clone();
    }
}
