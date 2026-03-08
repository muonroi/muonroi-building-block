using Muonroi.Governance.Compliance;
using Muonroi.Logging.Abstractions;

namespace Muonroi.Governance.Enterprise.Compliance;

public sealed class MComplianceExportHostedService(
    IMComplianceExportService exportService,
    LicenseConfigs configs,
    IMLog<MComplianceExportHostedService>? logger = null) : BackgroundService
{
    private readonly IMComplianceExportService _exportService =
        exportService ?? throw new ArgumentNullException(nameof(exportService));
    private readonly LicenseConfigs _configs = configs ?? throw new ArgumentNullException(nameof(configs));
    private readonly IMLog<MComplianceExportHostedService>? _logger = logger;

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
