using StackExchange.Redis;
using Muonroi.Core.Abstractions.Guards;

namespace Muonroi.Caching.Redis.Tenancy;

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

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisTenantCache"/> class.
    /// </summary>
    /// <param name="connection">The Redis connection.</param>
    /// <param name="tenantId">The tenant identifier.</param>
    public RedisTenantCache(IConnectionMultiplexer connection, string tenantId)
    {
        MGuard.NotNull(connection);
        MGuard.NotEmpty(tenantId);

        _database = connection.GetDatabase();
        _server = connection.GetServer(connection.GetEndPoints()[0]);
        _tenantPrefix = $"tenant:{tenantId}:";
    }

    private string Namespaced(string key)
    {
        return _tenantPrefix + key;
    }

    /// <summary>
    /// Sets a value for the specified tenant-scoped key.
    /// </summary>
    /// <param name="key">The key to set.</param>
    /// <param name="value">The value to store.</param>
    /// <param name="expiry">Optional expiration.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task<bool> SetAsync(string key, string value, TimeSpan? expiry = null)
    {
        return _database.StringSetAsync(Namespaced(key), value, expiry);
    }

    /// <summary>
    /// Gets a value for the specified tenant-scoped key.
    /// </summary>
    /// <param name="key">The key to retrieve.</param>
    /// <returns>A task that represents the asynchronous operation with the value.</returns>
    public Task<RedisValue> GetAsync(string key)
    {
        return _database.StringGetAsync(Namespaced(key));
    }

    /// <summary>
    /// Removes the specified tenant-scoped key.
    /// </summary>
    /// <param name="key">The key to remove.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
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
