using Muonroi.RuleEngine.Proliferation;
using Muonroi.RuleEngine.Proliferation.Models;

namespace Quickstart.RuleEngine.Proliferation.Api;

/// <summary>
/// Standalone IExternalProjectConfigProvider used when no control-plane is present.
///
/// The proliferation engine's RoutingScenarioExecutor resolves this provider to
/// decide whether a scenario runs against an external project or the internal
/// executor. Returning null routes every scenario to internal execution — the
/// correct behaviour for a self-contained building-block sample.
/// </summary>
public sealed class StandaloneExternalProjectConfigProvider : IExternalProjectConfigProvider
{
    /// <inheritdoc/>
    public Task<ExternalProjectConfig?> GetConfigAsync(string? tenantId, CancellationToken ct = default)
        => Task.FromResult<ExternalProjectConfig?>(null);
}
