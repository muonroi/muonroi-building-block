namespace Muonroi.RuleEngine.CEP.Controllers;

/// <summary>
/// Wire format for CEP configuration management APIs.
/// </summary>
public sealed record CepConfigDto
{
    /// <summary>
    /// Configuration identifier.
    /// </summary>
    public string Id { get; init; } = string.Empty;
    /// <summary>
    /// Tenant identifier, if applicable.
    /// </summary>
    public string? TenantId { get; init; }
    /// <summary>
    /// Configuration display name.
    /// </summary>
    public string Name { get; init; } = string.Empty;
    /// <summary>
    /// Optional description for the configuration.
    /// </summary>
    public string? Description { get; init; }
    /// <summary>
    /// Window type name.
    /// </summary>
    public string WindowType { get; init; } = nameof(Muonroi.RuleEngine.CEP.WindowType.Sliding);
    /// <summary>
    /// Window size in seconds.
    /// </summary>
    public int WindowSizeSeconds { get; init; } = 60;
    /// <summary>
    /// Time-to-live in seconds.
    /// </summary>
    public int TimeToLiveSeconds { get; init; } = 300;
    /// <summary>
    /// Correlation key for grouping events.
    /// </summary>
    public string CorrelationKey { get; init; } = "default";
    /// <summary>
    /// Free-form metadata for the configuration.
    /// </summary>
    public Dictionary<string, string> Metadata { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>
    /// Creation timestamp in UTC.
    /// </summary>
    public DateTime CreatedAtUtc { get; init; }
    /// <summary>
    /// Last update timestamp in UTC.
    /// </summary>
    public DateTime UpdatedAtUtc { get; init; }
}

/// <summary>
/// Simulation request payload for CEP window testing.
/// </summary>
public sealed record CepSimulationRequest
{
    /// <summary>
    /// Events to simulate.
    /// </summary>
    public List<CepSimulationEvent> Events { get; init; } = [];
}

/// <summary>
/// Single event used by the CEP simulation endpoint.
/// </summary>
public sealed record CepSimulationEvent
{
    /// <summary>
    /// Optional event key.
    /// </summary>
    public string? Key { get; init; }
    /// <summary>
    /// Event timestamp in UTC.
    /// </summary>
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow; // MBB001-exempt: DTO boundary default
    /// <summary>
    /// Event payload values.
    /// </summary>
    public Dictionary<string, object?> Payload { get; init; } = [];
}

/// <summary>
/// Simulation response for a single processed event.
/// </summary>
public sealed record CepSimulationWindowResult
{
    /// <summary>
    /// Window key.
    /// </summary>
    public string Key { get; init; } = string.Empty;
    /// <summary>
    /// Window timestamp in UTC.
    /// </summary>
    public DateTime TimestampUtc { get; init; }
    /// <summary>
    /// Event count in the window.
    /// </summary>
    public int Count { get; init; }
    /// <summary>
    /// Timestamps included in the window.
    /// </summary>
    public IReadOnlyList<DateTime> WindowTimestampsUtc { get; init; } = [];
}

/// <summary>
/// Simulation summary returned by the CEP controller.
/// </summary>
public sealed record CepSimulationResponse
{
    /// <summary>
    /// Configuration identifier.
    /// </summary>
    public string Id { get; init; } = string.Empty;
    /// <summary>
    /// Configuration name.
    /// </summary>
    public string Name { get; init; } = string.Empty;
    /// <summary>
    /// Tenant identifier.
    /// </summary>
    public string TenantId { get; init; } = "_global";
    /// <summary>
    /// Correlation key used for simulation.
    /// </summary>
    public string CorrelationKey { get; init; } = "default";
    /// <summary>
    /// Total processed event count.
    /// </summary>
    public int ProcessedEvents { get; init; }
    /// <summary>
    /// Windowed results for the simulation.
    /// </summary>
    public IReadOnlyList<CepSimulationWindowResult> Windows { get; init; } = [];
}
