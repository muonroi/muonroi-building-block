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
}
