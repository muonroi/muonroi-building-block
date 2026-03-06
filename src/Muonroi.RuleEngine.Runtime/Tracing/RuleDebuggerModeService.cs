namespace Muonroi.RuleEngine.Runtime.Tracing;

public sealed class RuleDebuggerModeService(
    IConnectionMultiplexer connectionMultiplexer,
    IOptions<RuleTracingOptions> options) : IRuleDebuggerModeService
{
    private readonly IConnectionMultiplexer _connectionMultiplexer =
        connectionMultiplexer ?? throw new ArgumentNullException(nameof(connectionMultiplexer));
    private readonly RuleTracingOptions _options = options?.Value ?? new RuleTracingOptions();

    public async ValueTask<bool> IsDebugEnabledAsync(string tenantId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ct.ThrowIfCancellationRequested();

        StackExchange.Redis.IDatabase db = _connectionMultiplexer.GetDatabase(_options.Database);
        RedisValue value = await db.StringGetAsync(BuildModeKey(tenantId));
        return !value.IsNullOrEmpty;
    }

    public async ValueTask EnableAsync(string tenantId, TimeSpan duration, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ct.ThrowIfCancellationRequested();

        if (duration <= TimeSpan.Zero)
        {
            duration = TimeSpan.FromMinutes(30);
        }

        StackExchange.Redis.IDatabase db = _connectionMultiplexer.GetDatabase(_options.Database);
        await db.StringSetAsync(BuildModeKey(tenantId), "1", duration);
    }

    public async ValueTask DisableAsync(string tenantId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ct.ThrowIfCancellationRequested();

        StackExchange.Redis.IDatabase db = _connectionMultiplexer.GetDatabase(_options.Database);
        await db.KeyDeleteAsync(BuildModeKey(tenantId));
    }

    private string BuildModeKey(string tenantId)
    {
        return $"{_options.DebuggerKeyPrefix}:{tenantId.Trim()}";
    }
}
