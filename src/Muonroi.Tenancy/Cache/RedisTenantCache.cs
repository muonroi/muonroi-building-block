namespace Muonroi.Tenancy.Cache;

/// <summary>
/// Redis cache wrapper that isolates data per tenant by namespacing keys.
/// Keys are prefixed with <c>tenant:{tenantId}:</c> to allow Redis ACL key patterns
/// like <c>tenant:{tenantId}:*</c>. Avoids using the KEYS command by relying on
/// SCAN through <see cref="IServer.KeysAsync"/>.
/// </summary>
public class RedisTenantCache
{
    private readonly IDatabase _database;
    private readonly IServer _server;
    private readonly string _tenantPrefix;

    public RedisTenantCache(IConnectionMultiplexer connection, string tenantId)
    {
        ArgumentNullException.ThrowIfNull(connection);

        if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));

        _database = connection.GetDatabase();
        _server = connection.GetServer(connection.GetEndPoints()[0]);
        _tenantPrefix = $"tenant:{tenantId}:";
    }

    private string Namespaced(string key)
    {
        return _tenantPrefix + key;
    }

    public Task<bool> SetAsync(string key, string value, TimeSpan? expiry = null)
    {
        return _database.StringSetAsync(Namespaced(key), value, expiry);
    }

    public Task<RedisValue> GetAsync(string key)
    {
        return _database.StringGetAsync(Namespaced(key));
    }

    public Task<bool> RemoveAsync(string key)
    {
        return _database.KeyDeleteAsync(Namespaced(key));
    }

    /// <summary>
    /// Enumerates keys for the tenant using SCAN to avoid blocking Redis.
    /// </summary>
    public async IAsyncEnumerable<RedisKey> ScanKeysAsync(string pattern)
    {
        await foreach (RedisKey key in _server.KeysAsync(_database.Database, Namespaced(pattern))) yield return key;
    }
}