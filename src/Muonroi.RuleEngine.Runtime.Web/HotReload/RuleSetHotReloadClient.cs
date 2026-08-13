namespace Muonroi.RuleEngine.Runtime.Web.HotReload;

using Microsoft.AspNetCore.SignalR.Client;
using Muonroi.Core.Abstractions.Guards;
using Muonroi.Logging.Abstractions;
using Muonroi.RuleEngine.Runtime.Rules;

/// <summary>
/// Background service that listens for ruleset changes from Control Plane.
/// </summary>
public sealed class RuleSetHotReloadClient(
    RuleSetHotReloadOptions options,
    IRuleSetChangeHandlerClient changeHandler,
    IMLog<RuleSetHotReloadClient>? logger = null) : BackgroundService
{
    /// <summary>
    /// Connects to the Control Plane hub and forwards ruleset changes to the registered handler.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(options.ControlPlaneUrl))
        {
            logger?.Info("[RuleSetHotReload] Hot-reload disabled - ControlPlaneUrl not configured.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            string hubUrl = BuildHubUrl();
            HubConnection connection = BuildConnection();
            TaskCompletionSource closedSignal = new(TaskCreationOptions.RunContinuationsAsynchronously);

            connection.Reconnected += async _ =>
            {
                await SubscribeTenantGroupAsync(connection, CancellationToken.None);
            };

            connection.Closed += _ =>
            {
                closedSignal.TrySetResult();
                return Task.CompletedTask;
            };

            connection.On<RuleSetChangeEvent>("RuleSetChanged", evt =>
                changeHandler.OnRuleSetChangedAsync(evt, stoppingToken));

            try
            {
                await connection.StartAsync(stoppingToken);
                await SubscribeTenantGroupAsync(connection, stoppingToken);
                logger?.Info("[RuleSetHotReload] Connected to ruleset hot-reload hub at {HubUrl}.", hubUrl);

                await WaitForCloseOrCancellationAsync(closedSignal.Task, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger?.Warn("[RuleSetHotReload] Hot-reload connection failed: {Message}. Retrying in {DelaySeconds}s.",
                    ex.Message, options.ReconnectDelay.TotalSeconds);
                await DelayBeforeRetryAsync(stoppingToken);
            }
            finally
            {
                await DisposeConnectionAsync(connection);
            }
        }
    }

    private HubConnection BuildConnection()
    {
        IHubConnectionBuilder builder = new HubConnectionBuilder()
            .WithUrl(BuildHubUrl(), httpOptions =>
            {
                if (options.AccessTokenFactory is not null)
                {
                    httpOptions.AccessTokenProvider = options.AccessTokenFactory;
                }
            })
            .WithAutomaticReconnect();

        return builder.Build();
    }

    private string BuildHubUrl()
    {
        string trimmed = MGuard.NotNull(options.ControlPlaneUrl).Trim().TrimEnd('/');
        return trimmed.EndsWith("/hubs/ruleset-changes", StringComparison.OrdinalIgnoreCase)
            ? trimmed
            : $"{trimmed}/hubs/ruleset-changes";
    }

    private async Task SubscribeTenantGroupAsync(HubConnection connection, CancellationToken cancellationToken)
    {
        if (options.SubscribeAllTenants)
        {
            await connection.InvokeAsync("JoinAllTenantsGroup", cancellationToken);
            logger?.Debug("[RuleSetHotReload] Subscribed to all-tenants group (multi-tenant mode).");
            return;
        }

        if (string.IsNullOrWhiteSpace(options.TenantId))
        {
            return;
        }

        await connection.InvokeAsync("JoinTenantGroup", options.TenantId, cancellationToken);
        logger?.Debug("[RuleSetHotReload] Subscribed ruleset hot-reload client to tenant {TenantId}.", options.TenantId);
    }

    private async Task DelayBeforeRetryAsync(CancellationToken stoppingToken)
    {
        if (options.ReconnectDelay <= TimeSpan.Zero)
        {
            return;
        }

        await Task.Delay(options.ReconnectDelay, stoppingToken);
    }

    private static async Task WaitForCloseOrCancellationAsync(Task closedTask, CancellationToken cancellationToken)
    {
        Task delayTask = Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        await Task.WhenAny(closedTask, delayTask);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static async Task DisposeConnectionAsync(HubConnection connection)
    {
        try
        {
            await connection.StopAsync();
        }
        catch
        {
            // Ignore best-effort shutdown errors during reconnect loops.
        }

        await connection.DisposeAsync();
    }
}
