using Muonroi.Core.Abstractions.Guards;
using Muonroi.Governance.Abstractions.License;
using Muonroi.Governance.Compliance;
using Muonroi.Logging.Abstractions;

namespace Muonroi.Governance.Enterprise.Compliance;

/// <summary>
/// Represents the MCompliance Export Hosted Service.
/// </summary>
public sealed class MComplianceExportHostedService(
    IMComplianceExportService exportService,
    LicenseConfigs configs,
    IMLog<MComplianceExportHostedService>? logger = null) : BackgroundService
{
    private readonly IMComplianceExportService _exportService = MGuard.NotNull(exportService);
    private readonly LicenseConfigs _configs = MGuard.NotNull(configs);
    private readonly IMLog<MComplianceExportHostedService>? _logger = logger;

    /// <summary>
    /// Executes the Execute Async operation.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                MComplianceExportRunResult result = await _exportService.ExportAsync(stoppingToken);
                _logger?.LogInformation(
                    "[Compliance] Export run complete. Exported={ExportedCount}, Chain={ChainCount}, ControlPlane={ControlPlaneCount}, LastHash={LastHash}",
                    result.ExportedCount,
                    result.ChainEntryCount,
                    result.ControlPlaneAuditCount,
                    result.LastRecordHash);
            }
            catch (Exception ex)
            {
                _logger?.Error(ex, "Compliance export background run failed.");
            }

            int intervalMinutes = _configs.Compliance.ExportIntervalMinutes;
            if (intervalMinutes <= 0)
            {
                intervalMinutes = 15;
            }

            await Task.Delay(TimeSpan.FromMinutes(intervalMinutes), stoppingToken);
        }
    }
}
