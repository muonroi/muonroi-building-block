namespace Muonroi.Governance.Compliance;

public interface IMComplianceExportService
{
    bool IsEnabled { get; }

    Task<MComplianceExportRunResult> ExportAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MComplianceExportRecord>> GetExportRecordsAsync(
        MComplianceExportQuery query,
        CancellationToken cancellationToken = default);

    Task<MComplianceVerificationResult> VerifyAsync(
        MComplianceVerificationRequest? request = null,
        CancellationToken cancellationToken = default);

    Task<int> PruneEvidencePacksAsync(CancellationToken cancellationToken = default);
}
