namespace Muonroi.RuleEngine.CEP.Controllers;

/// <summary>
/// Wire format for CEP configuration management APIs.
/// </summary>
public sealed record CepConfigDto
{
    public string Id { get; init; } = string.Empty;
    public string? TenantId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string WindowType { get; init; } = nameof(Muonroi.RuleEngine.CEP.WindowType.Sliding);
    public int WindowSizeSeconds { get; init; } = 60;
    public int TimeToLiveSeconds { get; init; } = 300;
    public string CorrelationKey { get; init; } = "default";
    public Dictionary<string, string> Metadata { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public DateTime CreatedAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
}

/// <summary>
/// Simulation request payload for CEP window testing.
/// </summary>
public sealed record CepSimulationRequest
{
    public List<CepSimulationEvent> Events { get; init; } = [];
}

/// <summary>
/// Single event used by the CEP simulation endpoint.
/// </summary>
public sealed record CepSimulationEvent
{
    public string? Key { get; init; }
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow; // MBB001-exempt: DTO boundary default
    public Dictionary<string, object?> Payload { get; init; } = [];
}

/// <summary>
/// Simulation response for a single processed event.
/// </summary>
public sealed record CepSimulationWindowResult
{
    public string Key { get; init; } = string.Empty;
    public DateTime TimestampUtc { get; init; }
    public int Count { get; init; }
    public IReadOnlyList<DateTime> WindowTimestampsUtc { get; init; } = [];
}

/// <summary>
/// Simulation summary returned by the CEP controller.
/// </summary>
public sealed record CepSimulationResponse
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string TenantId { get; init; } = "_global";
    public string CorrelationKey { get; init; } = "default";
    public int ProcessedEvents { get; init; }
    public IReadOnlyList<CepSimulationWindowResult> Windows { get; init; } = [];
}
