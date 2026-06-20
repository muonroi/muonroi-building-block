using System.Diagnostics.CodeAnalysis;

namespace Muonroi.Pdf.Governance.Policies;

[SuppressMessage("Muonroi.CodeStandards", "MSTD0001",
    Justification = "PdfPolicyException is the public PDF-contract exception type; consumers catch it directly. Cannot change hierarchy in netstandard2.0 Pdf.Abstractions.")]
public sealed class SignedPdfCssPolicyDecorator(
    IPdfCssPolicy inner,
    PdfConfigs configs,
    Func<bool> signatureVerifier) : IPdfCssPolicy
{
    private readonly IPdfCssPolicy _inner = inner;
    private readonly PdfConfigs _configs = configs;
    private readonly Func<bool> _signatureVerifier = signatureVerifier;

    public string Id => _inner.Id;
    public PdfPolicyLimits Limits => _inner.Limits;

    public async ValueTask<PolicyValidationResult> ValidateAsync(
        IPdfDocumentContext documentContext,
        CancellationToken ct = default)
    {
        if (_configs.RequirePolicySignature && !_signatureVerifier())
        {
            throw new PdfPolicyException(new[]
            {
                new PolicyViolation(
                    "gov.policy.signature-invalid",
                    "Policy configuration signature is invalid or missing.",
                    PolicySeverity.Error,
                    PropertyName: "RequirePolicySignature",
                    RejectedValue: "unsigned",
                    SuggestedAlternative: "Sign the policy config with the Muonroi policy signing tool")
            });
        }

        return await _inner.ValidateAsync(documentContext, ct);
    }
}
