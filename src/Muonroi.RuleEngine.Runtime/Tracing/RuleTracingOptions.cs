namespace Muonroi.RuleEngine.Runtime.Tracing;

/// <summary>
/// Options controlling rule execution tracing and debugger mode.
/// </summary>
public sealed class RuleTracingOptions
{
    /// <summary>Configuration section name used to bind these options.</summary>
    public const string SectionName = "RuleTracing";

    /// <summary>Default time-to-live for trace entries.</summary>
    public TimeSpan DefaultTtl { get; set; } = TimeSpan.FromHours(24);
    /// <summary>Page size for Redis key scans.</summary>
    public int ScanPageSize { get; set; } = 200;
    /// <summary>Maximum number of trace entries returned per query.</summary>
    public int MaxQueryResults { get; set; } = 1000;
    /// <summary>Redis database index to use for trace data.</summary>
    public int Database { get; set; }
    /// <summary>Key prefix for trace entries.</summary>
    public string TraceKeyPrefix { get; set; } = "rule-trace";
    /// <summary>Key prefix for debugger enablement flags.</summary>
    public string DebuggerKeyPrefix { get; set; } = "rule-debugger:enabled";
    /// <summary>Duration to cache debugger mode lookups.</summary>
    public TimeSpan ModeCacheDuration { get; set; } = TimeSpan.FromSeconds(10);
}
