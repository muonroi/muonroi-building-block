using Muonroi.Logging.Abstractions;
using Muonroi.RuleEngine.Proliferation.Models;

namespace Muonroi.RuleEngine.Proliferation.Execution;

/// <summary>
/// Decorator that routes scenario execution to either internal (DryRunAsync) or external
/// (HTTP endpoint) executor based on whether a registered external project exists for the
/// scenario's TenantId.
///
/// Routing decision:
/// - TenantId matches a registered external project → ExternalScenarioExecutor
/// - TenantId has no external project (or is null) → internal IScenarioExecutor
///
/// After external execution, optionally fires a webhook notification (fire-and-forget).
/// Webhook failures are caught/logged and never propagate to callers.
/// </summary>
/// <remarks>Creates a routing executor.</remarks>
public sealed class RoutingScenarioExecutor(
    IScenarioExecutor internalExecutor,
    IExternalScenarioExecutor externalExecutor,
    IExternalProjectConfigProvider configProvider,
    IWebhookNotificationService? webhookService = null,
    IMLog<RoutingScenarioExecutor>? logger = null) : IScenarioExecutor
{
    private readonly IScenarioExecutor _internalExecutor = internalExecutor;
    private readonly IExternalScenarioExecutor _externalExecutor = externalExecutor;
    private readonly IExternalProjectConfigProvider _configProvider = configProvider;
    private readonly IWebhookNotificationService? _webhookService = webhookService;
    private readonly IMLog<RoutingScenarioExecutor>? _logger = logger;

    /// <summary>
    /// Routes the scenario to internal or external executor.
    /// After external execution, fires webhook notification if configured (fire-and-forget).
    /// </summary>
    public async Task<ScenarioResult> ExecuteAsync(NeuronScenario scenario, CancellationToken ct = default)
    {
        ExternalProjectConfig? externalConfig = await _configProvider.GetConfigAsync(scenario.TenantId, ct);

        if (externalConfig == null)
        {
            // No external project registered for this tenant — route to internal executor
            _logger?.Debug("Scenario {ScenarioId} routed to internal executor (no external config for tenant {TenantId})",
                scenario.Id, scenario.TenantId ?? "<null>");
            return await _internalExecutor.ExecuteAsync(scenario, ct);
        }

        // External project registered — route to external executor
        _logger?.Debug("Scenario {ScenarioId} routed to external executor for tenant {TenantId} at {Endpoint}",
            scenario.Id, scenario.TenantId, externalConfig.ExecutionEndpointUrl);

        ScenarioResult result = await _externalExecutor.ExecuteAsync(scenario, externalConfig, ct);

        // Fire-and-forget webhook notification (never blocks or throws)
        if (!string.IsNullOrWhiteSpace(externalConfig.WebhookUrl) && _webhookService != null)
        {
            _ = FireWebhookAsync(externalConfig.WebhookUrl, scenario, result, ct);
        }

        return result;
    }

    private async Task FireWebhookAsync(
        string webhookUrl,
        NeuronScenario scenario,
        ScenarioResult result,
        CancellationToken ct)
    {
        if (_webhookService is null)
            return;

        try
        {
            WebhookPayload payload = new()
            {
                ScenarioId = scenario.Id,
                WorkflowName = scenario.SeedRuleCode,
                Status = result.IsSuccess ? "passed" : "failed",
                InputFacts = scenario.InputFacts,
                OutputFacts = result.OutputFacts,
                Duration = result.Duration,
                MatchesExpectation = result.MatchesExpectation,
                ExecutedAt = result.ExecutedAt
            };

            await _webhookService.NotifyAsync(webhookUrl, payload, ct);

            _logger?.Debug("Webhook notification sent to {WebhookUrl} for scenario {ScenarioId}",
                webhookUrl, scenario.Id);
        }
        catch (Exception ex)
        {
            // Webhook failure must never propagate — log and continue
            _logger?.Warn("Webhook notification to {WebhookUrl} failed for scenario {ScenarioId}: {Message}",
                webhookUrl, scenario.Id, ex.Message);
        }
    }
}
