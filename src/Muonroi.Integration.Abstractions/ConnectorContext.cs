using System.Text.Json;
using Muonroi.RuleEngine.Abstractions;

namespace Muonroi.Integration.Abstractions;

/// <summary>
/// Execution context passed to a connector. Contains per-instance config,
/// input facts from the FactBag, resolved credentials, and tenant context.
/// </summary>
public sealed class ConnectorContext
{
    /// <summary>Connector-specific configuration payload.</summary>
    public required JsonDocument Config { get; init; }
    /// <summary>Input facts supplied to the connector.</summary>
    public required FactBag InputFacts { get; init; }
    /// <summary>Resolved credential values for the connector.</summary>
    public required IReadOnlyDictionary<string, string> Credentials { get; init; }
    /// <summary>Tenant identifier associated with the request, if any.</summary>
    public required string? TenantId { get; init; }
    /// <summary>Correlation identifier for the current execution, if any.</summary>
    public required string? CorrelationId { get; init; }
}
