using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using Muonroi.Core.Abstractions.Interfaces;
using StackExchange.Redis;

namespace Muonroi.Caching.Redis.Routing;

/// <summary>
/// Redis-backed implementation of <see cref="IRedisRoutingTableStore"/> with local in-process caching.
/// </summary>
public sealed class RedisRoutingTableStore : IRedisRoutingTableStore, IDisposable
{
    private readonly IDatabase _database;
    private readonly ISubscriber _subscriber;
    private readonly IMJsonSerializeService _jsonSerializeService;
    private readonly IMDateTimeService _dateTimeService;
    private readonly RedisRoutingTableOptions _options;
    private readonly ConcurrentDictionary<string, CacheEntry> _localCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _subscriptionPattern;
    private int _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisRoutingTableStore"/> class.
    /// </summary>
    /// <param name="connectionMultiplexer">The shared Redis connection.</param>
    /// <param name="jsonSerializeService">The JSON serializer used for hash payloads.</param>
    /// <param name="dateTimeService">The date/time service used for TTL calculations.</param>
    /// <param name="options">The routing table options.</param>
    public RedisRoutingTableStore(
        IConnectionMultiplexer connectionMultiplexer,
        IMJsonSerializeService jsonSerializeService,
        IMDateTimeService dateTimeService,
        IOptions<RedisRoutingTableOptions>? options = null)
    {
        ArgumentNullException.ThrowIfNull(connectionMultiplexer);
        _jsonSerializeService = jsonSerializeService ?? throw new ArgumentNullException(nameof(jsonSerializeService));
        _dateTimeService = dateTimeService ?? throw new ArgumentNullException(nameof(dateTimeService));
        _options = options?.Value ?? new RedisRoutingTableOptions();
        _database = connectionMultiplexer.GetDatabase();
        _subscriber = connectionMultiplexer.GetSubscriber();
        _subscriptionPattern = BuildChannelPattern(_options.ChannelName);

        _subscriber.Subscribe(RedisChannel.Pattern(_subscriptionPattern), (_, message) =>
        {
            HandleInvalidation(message.ToString());
        });
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RoutingTableEntry>> GetRoutesAsync(string messageType, string tenantId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        string normalizedMessageType = NormalizeRequired(messageType, nameof(messageType));
        string normalizedTenantId = NormalizeRequired(tenantId, nameof(tenantId));
        string cacheKey = BuildLocalCacheKey(normalizedTenantId, normalizedMessageType);
        DateTimeOffset now = GetUtcNow();

        if (_localCache.TryGetValue(cacheKey, out CacheEntry? cacheEntry) && cacheEntry.ExpiresAt > now)
        {
            return cacheEntry.Entries;
        }

        RedisValue[] values = await _database.HashValuesAsync(BuildRedisHashKey(normalizedTenantId, normalizedMessageType));
        List<RoutingTableEntry> entries = [];
        foreach (RedisValue value in values)
        {
            if (value.IsNullOrEmpty)
            {
                continue;
            }

            RoutingTableEntry? entry = _jsonSerializeService.Deserialize<RoutingTableEntry>(value.ToString());
            if (entry?.IsActive == true)
            {
                entries.Add(entry);
            }
        }

        List<RoutingTableEntry> orderedEntries = entries
            .OrderBy(entry => entry.Order)
            .ThenBy(entry => entry.RuleCode, StringComparer.OrdinalIgnoreCase)
            .ToList();

        _localCache[cacheKey] = new CacheEntry(now.Add(_options.LocalCacheTtl), orderedEntries);
        return orderedEntries;
    }

    /// <inheritdoc />
    public async Task UpsertRouteAsync(RoutingTableEntry entry, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        RoutingTableEntry normalizedEntry = NormalizeEntry(entry);
        string hashKey = BuildRedisHashKey(normalizedEntry.TenantId, normalizedEntry.MessageType);
        string payload = _jsonSerializeService.Serialize(normalizedEntry);

        await _database.HashSetAsync(hashKey, normalizedEntry.RuleCode, payload);
        await PublishInvalidationAsync(normalizedEntry.TenantId, normalizedEntry.MessageType);
    }

    /// <inheritdoc />
    public async Task RemoveRouteAsync(string messageType, string tenantId, string ruleCode, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        string normalizedMessageType = NormalizeRequired(messageType, nameof(messageType));
        string normalizedTenantId = NormalizeRequired(tenantId, nameof(tenantId));
        string normalizedRuleCode = NormalizeRequired(ruleCode, nameof(ruleCode));

        await _database.HashDeleteAsync(BuildRedisHashKey(normalizedTenantId, normalizedMessageType), normalizedRuleCode);
        await PublishInvalidationAsync(normalizedTenantId, normalizedMessageType);
    }

    /// <summary>
    /// Disposes the store and unsubscribes the invalidation subscription.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        _subscriber.Unsubscribe(RedisChannel.Pattern(_subscriptionPattern));
    }

    private async Task PublishInvalidationAsync(string tenantId, string messageType)
    {
        string localCacheKey = BuildLocalCacheKey(tenantId, messageType);
        _localCache.TryRemove(localCacheKey, out _);

        string channel = BuildInvalidationChannel(_options.ChannelName, tenantId, messageType);
        await _subscriber.PublishAsync(RedisChannel.Literal(channel), localCacheKey);
    }

    private void HandleInvalidation(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return;
        }

        _localCache.TryRemove(payload.Trim(), out _);
    }

    private RoutingTableEntry NormalizeEntry(RoutingTableEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return entry with
        {
            MessageType = NormalizeRequired(entry.MessageType, nameof(entry.MessageType)),
            TenantId = NormalizeRequired(entry.TenantId, nameof(entry.TenantId)),
            RuleCode = NormalizeRequired(entry.RuleCode, nameof(entry.RuleCode)),
            TargetAddress = NormalizeRequired(entry.TargetAddress, nameof(entry.TargetAddress)),
            FeelExpression = NormalizeOptional(entry.FeelExpression)
        };
    }

    private static string NormalizeRequired(string value, string paramName)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Value is required.", paramName)
            : value.Trim();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private string BuildRedisHashKey(string tenantId, string messageType)
    {
        return $"{_options.KeyPrefix}:{tenantId}:{messageType}";
    }

    private static string BuildInvalidationChannel(string channelName, string tenantId, string messageType)
    {
        return $"{channelName}:{tenantId}:{messageType}";
    }

    private static string BuildChannelPattern(string channelName)
    {
        return $"{channelName}:*";
    }

    private static string BuildLocalCacheKey(string tenantId, string messageType)
    {
        return $"{tenantId}|{messageType}";
    }

    private DateTimeOffset GetUtcNow()
    {
        return new DateTimeOffset(_dateTimeService.UtcNow(), TimeSpan.Zero);
    }

    private sealed record CacheEntry(DateTimeOffset ExpiresAt, IReadOnlyList<RoutingTableEntry> Entries);
}
