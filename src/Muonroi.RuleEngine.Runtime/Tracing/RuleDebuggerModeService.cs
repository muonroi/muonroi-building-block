using Muonroi.Core.Abstractions.Guards;

namespace Muonroi.RuleEngine.Runtime.Tracing;

/// <summary>
/// Stores and queries debugger enablement flags for tenants.
/// </summary>
public sealed class RuleDebuggerModeService(
    IConnectionMultiplexer connectionMultiplexer,
    IOptions<RuleTracingOptions> options) : IRuleDebuggerModeService
{
    private readonly IConnectionMultiplexer _connectionMultiplexer =
        MGuard.NotNull(connectionMultiplexer);
    private readonly RuleTracingOptions _options = options?.Value ?? new RuleTracingOptions();

    /// <summary>Returns true if debugging is enabled for the tenant.</summary>
    public async ValueTask<bool> IsDebugEnabledAsync(string tenantId, CancellationToken ct = default)
    {
        MGuard.NotEmpty(tenantId);
        ct.ThrowIfCancellationRequested();

        StackExchange.Redis.IDatabase db = _connectionMultiplexer.GetDatabase(_options.Database);
        RedisValue value = await db.StringGetAsync(BuildModeKey(tenantId));
        return !value.IsNullOrEmpty;
    }

    /// <summary>Enables debugging for the tenant for the specified duration.</summary>
    public async ValueTask EnableAsync(string tenantId, TimeSpan duration, CancellationToken ct = default)
    {
        MGuard.NotEmpty(tenantId);
        ct.ThrowIfCancellationRequested();

        if (duration <= TimeSpan.Zero)
        {
            duration = TimeSpan.FromMinutes(30);
        }

        StackExchange.Redis.IDatabase db = _connectionMultiplexer.GetDatabase(_options.Database);
        await db.StringSetAsync(BuildModeKey(tenantId), "1", duration);
    }

    /// <summary>Disables debugging for the tenant.</summary>
    public async ValueTask DisableAsync(string tenantId, CancellationToken ct = default)
    {
        MGuard.NotEmpty(tenantId);
        ct.ThrowIfCancellationRequested();

        StackExchange.Redis.IDatabase db = _connectionMultiplexer.GetDatabase(_options.Database);
        await db.KeyDeleteAsync(BuildModeKey(tenantId));
    }

    private string BuildModeKey(string tenantId)
    {
        return $"{_options.DebuggerKeyPrefix}:{tenantId.Trim()}";
    }
}
