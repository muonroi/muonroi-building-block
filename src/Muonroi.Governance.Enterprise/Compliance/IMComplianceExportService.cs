namespace Muonroi.Governance.Compliance;

/// <summary>
/// Represents the IMCompliance Export Service.
/// </summary>
public interface IMComplianceExportService
{
    /// <summary>
    /// Gets the Is Enabled.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Executes the Export Async operation.
    /// </summary>
    Task<MComplianceExportRunResult> ExportAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the Get Export Records Async operation.
    /// </summary>
    Task<IReadOnlyList<MComplianceExportRecord>> GetExportRecordsAsync(
        MComplianceExportQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the Verify Async operation.
    /// </summary>
    Task<MComplianceVerificationResult> VerifyAsync(
        MComplianceVerificationRequest? request = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the Prune Evidence Packs Async operation.
    /// </summary>
    Task<int> PruneEvidencePacksAsync(CancellationToken cancellationToken = default);
}
