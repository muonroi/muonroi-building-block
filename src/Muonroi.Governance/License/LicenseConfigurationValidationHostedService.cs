using Muonroi.Governance.Abstractions.License;
using Muonroi.Logging.Abstractions;

namespace Muonroi.Governance.License;

internal sealed class LicenseConfigurationValidationHostedService(
    LicenseConfigs configs,
    LicenseState state,
    IHostEnvironment environment,
    IMLog<LicenseConfigurationValidationHostedService>? logger = null) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        ValidateConfiguration();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private void ValidateConfiguration()
    {
        if (configs.Mode == LicenseMode.Offline)
        {
            logger?.Info("[License] Running in Offline mode.");
        }

        string? proofPath = configs.ActivationProofPath;
        if (!string.IsNullOrWhiteSpace(proofPath))
        {
            string resolvedProofPath = Path.IsPathRooted(proofPath)
                ? proofPath
                : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, proofPath));

            bool proofRequired = configs.Mode == LicenseMode.Online && !configs.FallbackToOnlineActivation;
            if (proofRequired && !File.Exists(resolvedProofPath))
            {
                throw new LicenseException(
                    $"[LICENSE] Activation proof file was not found at '{resolvedProofPath}'. Re-activate the license or enable fallback online activation.");
            }
        }

        if (!environment.IsProduction())
        {
            return;
        }

        LicenseTier tier = state.TrustedTier;
        if (tier != LicenseTier.Enterprise)
        {
            return;
        }

        if (!configs.EnableServerValidation)
        {
            logger?.Warn("[License] Enterprise tier is running without server validation.");
        }

        if (!configs.EnableAntiTampering)
        {
            logger?.Warn("[License] Enterprise tier is running without anti-tampering protection.");
        }
    }
}
