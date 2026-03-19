namespace Muonroi.RuleEngine.CEP;

/// <summary>
/// Describes a persisted CEP window configuration.
/// </summary>
public sealed record CepConfig
{
    /// <summary>
    /// Unique identifier of the configuration.
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Tenant partition that owns the configuration.
    /// </summary>
    public string TenantId { get; init; } = "_global";

    /// <summary>
    /// Human-readable name shown to operators.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Optional free-form description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Windowing strategy.
    /// </summary>
    public WindowType WindowType { get; init; } = WindowType.Sliding;

    /// <summary>
    /// Active window size.
    /// </summary>
    public TimeSpan WindowSize { get; init; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Retention period for historical events.
    /// </summary>
    public TimeSpan TimeToLive { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Logical field used to correlate events.
    /// </summary>
    public string CorrelationKey { get; init; } = "default";

    /// <summary>
    /// Additional metadata for downstream consumers.
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Creation timestamp in UTC.
    /// </summary>
    public DateTime CreatedAtUtc { get; init; }

    /// <summary>
    /// Last update timestamp in UTC.
    /// </summary>
    public DateTime UpdatedAtUtc { get; init; }
}
