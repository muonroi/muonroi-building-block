namespace Muonroi.Governance.Compliance;

/// <summary>
/// Represents the IMCompliance Evidence Pack Service.
/// </summary>
public interface IMComplianceEvidencePackService
{
    /// <summary>
    /// Executes the Generate Async operation.
    /// </summary>
    Task<MComplianceEvidencePackResult> GenerateAsync(
        MComplianceEvidencePackRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads a previously generated evidence pack from <paramref name="packFilePath"/> and verifies
    /// its authenticity (signature over the stored pack hash) and, when records are embedded, its
    /// content integrity (recomputed hash matches the stored pack hash). Used for audit defensibility:
    /// detects post-generation tampering of the pack file.
    /// </summary>
    Task<MComplianceEvidencePackVerifyResult> VerifyAsync(
        string packFilePath,
        CancellationToken cancellationToken = default);
}
