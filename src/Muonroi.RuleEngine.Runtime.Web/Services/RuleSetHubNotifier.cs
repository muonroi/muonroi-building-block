using Muonroi.Core.Abstractions.Ecosystem;
using Muonroi.Logging.Abstractions;
using Muonroi.RuleEngine.Runtime.Web.Hubs;

namespace Muonroi.RuleEngine.Runtime.Web.Services;

/// <summary>
/// Bridges ruleset change notifications to SignalR clients.
/// When the MultiTenant capability is active and a TenantId is present, broadcasts only
/// to the tenant-specific group and the global all-tenants group. Otherwise falls back
/// to <see cref="IHubClients.All"/> for backward compatibility.
/// </summary>
/// <param name="notifier">Ruleset change notifier.</param>
/// <param name="hubContext">SignalR hub context.</param>
/// <param name="logger">Logger for notification failures.</param>
/// <param name="ecosystemRegistry">Optional ecosystem capability registry. When null, single-tenant fallback applies.</param>
public sealed class RuleSetHubNotifier(
    IRuleSetChangeNotifier notifier,
    IHubContext<RuleSetChangeHub> hubContext,
    IMLog<RuleSetHubNotifier> logger,
    IMEcosystemRegistry? ecosystemRegistry = null) : IHostedService, IDisposable
{
    private IDisposable? _subscription;
    private readonly IMEcosystemRegistry? _ecosystemRegistry = ecosystemRegistry;

    /// <summary>Starts listening to rule change events and publishing to clients.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the operation.</returns>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _subscription = notifier.Subscribe(NotifyClientsAsync);
        logger?.Info("RuleSetHubNotifier subscribed to rule change events.");
        return Task.CompletedTask;
    }

    /// <summary>Stops listening to rule change events.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the operation.</returns>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _subscription?.Dispose();
        _subscription = null;
        return Task.CompletedTask;
    }

    /// <summary>Disposes the notifier subscription.</summary>
    public void Dispose()
    {
        _subscription?.Dispose();
    }

    private async Task NotifyClientsAsync(RuleSetChangeEvent changeEvent)
    {
        try
        {
            if (_ecosystemRegistry?.Has(MCapability.MultiTenant) == true
                && !string.IsNullOrWhiteSpace(changeEvent.TenantId))
            {
                // Multi-tenant mode: route to scoped groups only
                string tenantId = changeEvent.TenantId;
                await hubContext.Clients
                    .Group(RuleSetChangeHub.BuildTenantGroup(tenantId))
                    .SendAsync("RuleSetChanged", changeEvent);

                await hubContext.Clients
                    .Group(RuleSetChangeHub.AllTenantsGroup)
                    .SendAsync("RuleSetChanged", changeEvent);
            }
            else
            {
                // Single-tenant or no capability: broadcast to all
                await hubContext.Clients.All
                    .SendAsync("RuleSetChanged", changeEvent);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to push RuleSetChanged event for workflow '{WorkflowName}'.",
                changeEvent.WorkflowName);
        }
    }
}
