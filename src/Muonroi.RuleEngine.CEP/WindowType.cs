namespace Muonroi.RuleEngine.CEP;

/// <summary>
/// Supported windowing strategies for the CEP engine.
/// </summary>
public enum WindowType
{
    /// <summary>A sliding window moves over time with a configurable step.</summary>
    Sliding,
    /// <summary>A tumbling window groups events into non-overlapping buckets.</summary>
    Tumbling
}
