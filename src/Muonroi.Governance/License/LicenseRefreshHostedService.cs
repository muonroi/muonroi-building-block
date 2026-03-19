using Muonroi.Governance.Abstractions.License;
using Muonroi.Logging.Abstractions;

namespace Muonroi.Governance.License;

/// <summary>
/// Background service that periodically refreshes the license from the server.
/// This enables:
/// 1. Server-side license revocation
/// 2. Feature updates without restart
/// 3. License renewal before expiration
/// </summary>
public sealed class LicenseRefreshHostedService(
    ILicenseActivationService activationService,
    LicenseConfigs configs,
    LicenseStateNotifier stateNotifier,
    IMLog<LicenseRefreshHostedService>? logger = null)
    : BackgroundService
{
    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Only run if online mode is configured
        if (configs.Mode != LicenseMode.Online || string.IsNullOrWhiteSpace(configs.Online.Endpoint))
        {
            logger?.Info("[License] Offline mode - background refresh disabled.");
            return;
        }

        TimeSpan refreshInterval = TimeSpan.FromMinutes(
            configs.Online.RefreshMinutes > 0 ? configs.Online.RefreshMinutes : 1440);

        logger?.Info("[License] Background refresh enabled. Interval: {Interval}", refreshInterval);

        // Initial delay to allow app startup
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                logger?.Debug("[License] Refreshing license from server...");
                LicenseActivationResult result = await activationService.RefreshAsync(stoppingToken);

                if (result.IsSuccess)
                {
                    object expiryValue = result.Payload?.ExpiresAt is { } expiresAt
                        ? expiresAt
                        : "<unknown>";
                    logger?.Info("[License] License refreshed successfully. Expires: {Expiry}",
                        expiryValue);
                    stateNotifier.NotifyRefreshed(result.Payload!);
                }
                else
                {
                    object errorValue = result.Error ?? "<unknown>";
                    logger?.Warn("[License] Refresh failed: {Error}. Continuing with cached license.",
                        errorValue);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger?.Error(ex, "[License] Unexpected error during refresh.");
            }

            await Task.Delay(refreshInterval, stoppingToken);
        }
    }
}

/// <summary>
/// Notifies components when license state changes.
/// Allows runtime update of license state without restart.
/// </summary>
public sealed class LicenseStateNotifier
{
    private readonly object _lock = new();
    private LicensePayload? _latestPayload;

    /// <inheritdoc/>
    public event Action<LicensePayload>? OnLicenseRefreshed;

    /// <inheritdoc/>
    public LicensePayload? LatestPayload
    {
        get
        {
            lock (_lock)
            {
                return _latestPayload;
            }
        }
    }

    /// <inheritdoc/>
    public void NotifyRefreshed(LicensePayload payload)
    {
        lock (_lock)
        {
            _latestPayload = payload;
        }
        OnLicenseRefreshed?.Invoke(payload);
    }
}
