using Muonroi.Core.Abstractions.Diagnostics;

namespace Muonroi.RuleEngine.Runtime.Tracing;

public sealed class RuleExecutionTracer(
    IRuleTraceStore store,
    IRuleDebuggerModeService debuggerModeService,
    IOptions<RuleTracingOptions> options,
    IMTraceContext? traceContext = null) : IRuleExecutionTracer
{
    private readonly IRuleTraceStore _store = store ?? throw new ArgumentNullException(nameof(store));
    private readonly IRuleDebuggerModeService _debuggerModeService =
        debuggerModeService ?? throw new ArgumentNullException(nameof(debuggerModeService));
    private readonly RuleTracingOptions _options = options?.Value ?? new RuleTracingOptions();
    private readonly ConcurrentDictionary<string, (bool Enabled, DateTimeOffset ExpiresAt)> _modeCache = new(StringComparer.OrdinalIgnoreCase);

    public bool IsEnabled(string? tenantId)
    {
        // ── Per-request override ───────────────────────────────────────────────
        if (traceContext?.Current?.IsActive == true)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return false;
        }

        string normalizedTenant = tenantId.Trim();
        if (_modeCache.TryGetValue(normalizedTenant, out (bool Enabled, DateTimeOffset ExpiresAt) cached) &&
            cached.ExpiresAt > DateTimeOffset.UtcNow)
        {
            return cached.Enabled;
        }

        bool enabled = _debuggerModeService.IsDebugEnabledAsync(normalizedTenant).AsTask().GetAwaiter().GetResult();
        _modeCache[normalizedTenant] = (enabled, DateTimeOffset.UtcNow.Add(_options.ModeCacheDuration));
        return enabled;
    }

    public async ValueTask TraceAsync(RuleTraceEntry entry, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ct.ThrowIfCancellationRequested();

        if (!IsEnabled(entry.TenantId))
        {
            return;
        }

        await _store.SaveAsync(entry, _options.DefaultTtl, ct);
    }
}
