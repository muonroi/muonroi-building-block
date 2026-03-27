using Muonroi.Logging.Abstractions;
using Muonroi.RuleEngine.Runtime.Web.Hubs;

namespace Muonroi.RuleEngine.Runtime.Web.Services;

/// <summary>
/// Bridges ruleset change notifications to SignalR clients.
/// </summary>
/// <param name="notifier">Ruleset change notifier.</param>
/// <param name="hubContext">SignalR hub context.</param>
/// <param name="logger">Logger for notification failures.</param>
public sealed class RuleSetHubNotifier(
    IRuleSetChangeNotifier notifier,
    IHubContext<RuleSetChangeHub> hubContext,
    IMLog<RuleSetHubNotifier> logger) : IHostedService, IDisposable
{
    private IDisposable? _subscription;

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
            string tenantId = string.IsNullOrWhiteSpace(changeEvent.TenantId) ? "default" : changeEvent.TenantId;

            // Send to tenant-specific group (dashboard clients filtering by tenant)
            await hubContext.Clients
                .Group(RuleSetChangeHub.BuildTenantGroup(tenantId))
                .SendAsync("RuleSetChanged", changeEvent);

            // Send to all-tenants group (consumer apps serving multiple tenants)
            await hubContext.Clients
                .Group(RuleSetChangeHub.AllTenantsGroup)
                .SendAsync("RuleSetChanged", changeEvent);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to push RuleSetChanged event for workflow '{WorkflowName}'.", changeEvent.WorkflowName);
        }
    }
}

