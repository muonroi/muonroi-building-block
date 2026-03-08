using Muonroi.Logging.Abstractions;
using Muonroi.RuleEngine.Runtime.Web.Hubs;

namespace Muonroi.RuleEngine.Runtime.Web.Services;

public sealed class RuleSetHubNotifier(
    IRuleSetChangeNotifier notifier,
    IHubContext<RuleSetChangeHub> hubContext,
    IMLog<RuleSetHubNotifier> logger) : IHostedService, IDisposable
{
    private IDisposable? _subscription;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _subscription = notifier.Subscribe(NotifyClientsAsync);
        logger?.Info("RuleSetHubNotifier subscribed to rule change events.");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _subscription?.Dispose();
        _subscription = null;
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _subscription?.Dispose();
    }

    private async Task NotifyClientsAsync(RuleSetChangeEvent changeEvent)
    {
        try
        {
            string tenantId = string.IsNullOrWhiteSpace(changeEvent.TenantId) ? "default" : changeEvent.TenantId;
            await hubContext.Clients
                .Group(RuleSetChangeHub.BuildTenantGroup(tenantId))
                .SendAsync("RuleSetChanged", changeEvent);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to push RuleSetChanged event for workflow '{WorkflowName}'.", changeEvent.WorkflowName);
        }
    }
}

