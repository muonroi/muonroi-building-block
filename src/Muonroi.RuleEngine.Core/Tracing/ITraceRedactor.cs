namespace Muonroi.RuleEngine.Core.Tracing;

/// <summary>
/// Redacts PII/sensitive data from trace entries before persistence.
/// </summary>
public interface ITraceRedactor
{
    /// <summary>
    /// Returns a redacted copy of the trace entry.
    /// The original entry MUST NOT be mutated (RuleTraceEntry is a record).
    /// </summary>
    RuleTraceEntry Redact(RuleTraceEntry entry);
}
