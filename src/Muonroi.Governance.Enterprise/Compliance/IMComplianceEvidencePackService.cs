namespace Muonroi.Governance.Compliance;

public interface IMComplianceEvidencePackService
{
    Task<MComplianceEvidencePackResult> GenerateAsync(
        MComplianceEvidencePackRequest request,
        CancellationToken cancellationToken = default);
}
