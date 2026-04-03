using Muonroi.Governance.Abstractions.License;
using Muonroi.Logging.Abstractions;

namespace Muonroi.Governance.Enterprise.ServerValidation;

/// <summary>
/// Background service that periodically submits action chains to the license server.
/// </summary>
public sealed class ChainSubmissionHostedService(
    IServiceProvider serviceProvider,
    LicenseConfigs configs,
    IMLog<ChainSubmissionHostedService>? logger = null,
    LicenseState? licenseState = null)
    : BackgroundService
{
    private readonly LicenseState _licenseState = licenseState ?? LicenseState.CreateFree();
    private readonly ConcurrentDictionary<string, long> _lastSubmittedSequenceByTenant = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configs.EnableServerValidation)
        {
            logger?.LogInformation("[License] Server validation is disabled. Background submission service will not run.");
            return;
        }

        logger?.LogInformation("[License] Starting background action chain submission service...");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Wait for the configured interval
                await Task.Delay(TimeSpan.FromMinutes(configs.ChainSubmissionIntervalMinutes), stoppingToken);

                using IServiceScope scope = serviceProvider.CreateScope();
                if (!HasAuditTrailFeature(scope.ServiceProvider))
                {
                    logger?.LogDebug(
                        "[License] Skipping chain submission tick because 'audit-trail' feature is not licensed.");
                    continue;
                }

                IFingerprintChainStore chainStore = scope.ServiceProvider.GetRequiredService<IFingerprintChainStore>();
                NonceRotator nonceRotator = scope.ServiceProvider.GetRequiredService<NonceRotator>();
                List<string> tenants = [.. chainStore.GetTenantPartitions()];
                if (tenants.Count == 0)
                {
                    tenants.Add(AuditTrailTenantPartition.HostPartition);
                }

                foreach (string? tenant in tenants)
                {
                    long lastSequence = _lastSubmittedSequenceByTenant.TryGetValue(tenant, out long stored) ? stored : 0;
                    List<FingerprintChainEntry> recentEntries = [.. chainStore.GetRecentEntries(configs.ChainSubmissionBatchSize, lastSequence, tenant)];

                    if (!recentEntries.Any())
                    {
                        continue;
                    }

                    await nonceRotator.RotateAsync(recentEntries, tenant, stoppingToken);
                    _lastSubmittedSequenceByTenant[tenant] = recentEntries.Max(e => e.Sequence);
                }
            }
            catch (OperationCanceledException)
            {
                // Graceful shutdown
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "[License] Error in background action chain submission.");
            }
        }
    }

    private bool HasAuditTrailFeature(IServiceProvider serviceProvider)
    {
        ILicenseGuard? guard = serviceProvider.GetService<ILicenseGuard>();
        if (guard is null)
        {
            return _licenseState.HasFeature(FreeTierFeatures.Premium.AuditTrail);
        }

        try
        {
            guard.EnsureFeature(FreeTierFeatures.Premium.AuditTrail);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
