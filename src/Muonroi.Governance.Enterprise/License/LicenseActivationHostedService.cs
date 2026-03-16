using Muonroi.Governance.Abstractions.License;
using Muonroi.Logging.Abstractions;

namespace Muonroi.Governance.Enterprise.License;

/// <summary>
/// Hosted service that runs license activation on startup when Mode=Online
/// and no valid activation proof exists yet.
/// </summary>
internal sealed class LicenseActivationHostedService(
    LicenseActivator activator,
    LicenseConfigs configs,
    LicenseState licenseState,
    IMLog<LicenseActivationHostedService>? logger = null) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (configs.Mode != LicenseMode.Online || string.IsNullOrWhiteSpace(configs.Online.Endpoint))
        {
            return;
        }

        // Skip if already activated with valid Enterprise tier
        if (licenseState.IsValid && licenseState.Tier >= LicenseTier.Licensed)
        {
            logger?.Debug("[License] Already activated with valid tier {Tier}. Skipping activation.", licenseState.Tier);
            return;
        }

        logger?.Info("[License] No valid activation found. Attempting online activation...");
        bool success = await activator.TryActivateAsync(cancellationToken);

        if (success)
        {
            logger?.Info("[License] Online activation succeeded. Restart required to apply new license state.");
        }
        else
        {
            logger?.Warn("[License] Online activation failed. Running in restricted mode.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
