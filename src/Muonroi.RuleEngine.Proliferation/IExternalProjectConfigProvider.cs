using Muonroi.RuleEngine.Proliferation.Models;

namespace Muonroi.RuleEngine.Proliferation;

/// <summary>
/// Provides external project execution configuration based on tenant identifier.
/// The concrete implementation is registered by the control-plane (Plan 02).
/// When running without control-plane (standalone building-block), this interface
/// is not registered and RoutingScenarioExecutor falls back to internal execution.
/// </summary>
public interface IExternalProjectConfigProvider
{
    /// <summary>
    /// Returns the external project configuration for the given tenant, or null if
    /// no external project is registered for this tenant (routes to internal execution).
    /// </summary>
    /// <param name="tenantId">Tenant identifier from the scenario. May be null.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<ExternalProjectConfig?> GetConfigAsync(string? tenantId, CancellationToken ct = default);
}
