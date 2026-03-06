namespace Muonroi.RuleEngine.Runtime.Tracing;

public sealed class RuleTracingOptions
{
    public const string SectionName = "RuleTracing";

    public TimeSpan DefaultTtl { get; set; } = TimeSpan.FromHours(24);
    public int ScanPageSize { get; set; } = 200;
    public int MaxQueryResults { get; set; } = 1000;
    public int Database { get; set; }
    public string TraceKeyPrefix { get; set; } = "rule-trace";
    public string DebuggerKeyPrefix { get; set; } = "rule-debugger:enabled";
    public TimeSpan ModeCacheDuration { get; set; } = TimeSpan.FromSeconds(10);
}
