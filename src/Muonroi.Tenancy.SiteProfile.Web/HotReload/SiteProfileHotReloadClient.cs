namespace Muonroi.Tenancy.SiteProfile.Web.HotReload;

using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Hosting;
using Muonroi.Logging.Abstractions;

/// <summary>
/// Background service that listens for site profile changes from Control Plane.
/// Writes enable/disable state to ISiteProfileStateRegistry for per-request middleware checks.
/// </summary>
public sealed class SiteProfileHotReloadClient(
    SiteProfileHotReloadOptions options,
    ISiteProfileStateRegistry stateRegistry,
    ISiteProfileChangeHandler? changeHandler = null,
    IMLog<SiteProfileHotReloadClient>? logger = null) : BackgroundService
{
    /// <summary>
    /// Connects to the Control Plane SiteProfileChangeHub and forwards site state changes to the registry.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(options.ControlPlaneUrl))
        {
            logger?.Info("[SiteProfileHotReload] Hot-reload disabled - ControlPlaneUrl not configured.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            string hubUrl = BuildHubUrl();
            HubConnection connection = BuildConnection();
            TaskCompletionSource closedSignal = new(TaskCreationOptions.RunContinuationsAsynchronously);

            connection.Reconnected += async _ =>
            {
                await SubscribeAsync(connection, CancellationToken.None);
            };

            connection.Closed += _ =>
            {
                closedSignal.TrySetResult();
                return Task.CompletedTask;
            };

            connection.On<SiteProfileChangeEvent>("SiteProfileChanged", async evt =>
            {
                // Write to mutable state registry — this is what the middleware reads per-request
                stateRegistry.SetSiteEnabled(evt.SiteId, evt.IsEnabled);

                logger?.Debug("[SiteProfileHotReload] Site '{SiteId}' enabled state changed to {IsEnabled}.",
                    evt.SiteId, evt.IsEnabled);

                // Also notify any registered custom handlers
                if (changeHandler is not null)
                {
                    await changeHandler.OnSiteProfileChangedAsync(evt, stoppingToken);
                }
            });

            try
            {
                await connection.StartAsync(stoppingToken);
                await SubscribeAsync(connection, stoppingToken);
                logger?.Info("[SiteProfileHotReload] Connected to site-profile hot-reload hub at {HubUrl}.", hubUrl);

                await WaitForCloseOrCancellationAsync(closedSignal.Task, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger?.Warn("[SiteProfileHotReload] Hot-reload connection failed: {Message}. Retrying in {DelaySeconds}s.",
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
        return trimmed.EndsWith("/hubs/site-profile-changes", StringComparison.OrdinalIgnoreCase)
            ? trimmed
            : $"{trimmed}/hubs/site-profile-changes";
    }

    private static async Task SubscribeAsync(HubConnection connection, CancellationToken cancellationToken)
    {
        // Site profile changes are global (not per-tenant) — subscribe to the global group
        await connection.InvokeAsync("Subscribe", cancellationToken);
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
