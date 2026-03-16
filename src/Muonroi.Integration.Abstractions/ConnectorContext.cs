using System.Text.Json;
using Muonroi.RuleEngine.Abstractions;

namespace Muonroi.Integration.Abstractions;

/// <summary>
/// Execution context passed to a connector. Contains per-instance config,
/// input facts from the FactBag, resolved credentials, and tenant context.
/// </summary>
public sealed class ConnectorContext
{
    public required JsonDocument Config { get; init; }
    public required FactBag InputFacts { get; init; }
    public required IReadOnlyDictionary<string, string> Credentials { get; init; }
    public required string? TenantId { get; init; }
    public required string? CorrelationId { get; init; }
}
