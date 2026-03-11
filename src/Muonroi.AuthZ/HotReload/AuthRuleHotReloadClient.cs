namespace Muonroi.AuthZ.HotReload;

using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Hosting;
using Muonroi.Logging.Abstractions;

/// <summary>
/// Background service that listens for authorization rule changes from Control Plane.
/// </summary>
public sealed class AuthRuleHotReloadClient(
    AuthRuleHotReloadOptions options,
    IAuthRuleChangeHandler changeHandler,
    IMLog<AuthRuleHotReloadClient>? logger = null) : BackgroundService
{
    /// <summary>
    /// Connects to the Control Plane hub and forwards auth-rule changes to the registered handler.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(options.ControlPlaneUrl))
        {
            logger?.Info("[AuthZ] Hot-reload disabled - ControlPlaneUrl not configured.");
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

            connection.On<Guid>("AuthRuleChanged", ruleSetId =>
                changeHandler.OnAuthRuleChangedAsync(ruleSetId, stoppingToken));

            try
            {
                await connection.StartAsync(stoppingToken);
                await SubscribeTenantGroupAsync(connection, stoppingToken);
                logger?.Info("[AuthZ] Connected to auth-rule hot-reload hub at {HubUrl}.", hubUrl);

                await WaitForCloseOrCancellationAsync(closedSignal.Task, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger?.Warn("[AuthZ] Hot-reload connection failed: {Message}. Retrying in {DelaySeconds}s.",
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
        string trimmed = options.ControlPlaneUrl!.Trim().TrimEnd('/');
        return trimmed.EndsWith("/hubs/auth-rule-changes", StringComparison.OrdinalIgnoreCase)
            ? trimmed
            : $"{trimmed}/hubs/auth-rule-changes";
    }

    private async Task SubscribeTenantGroupAsync(HubConnection connection, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.TenantId))
        {
            return;
        }

        await connection.InvokeAsync("Subscribe", options.TenantId, cancellationToken);
        logger?.Debug("[AuthZ] Subscribed auth-rule hot-reload client to tenant {TenantId}.", options.TenantId);
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
