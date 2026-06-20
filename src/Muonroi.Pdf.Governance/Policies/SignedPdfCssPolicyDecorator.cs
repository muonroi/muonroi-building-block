using System.Diagnostics.CodeAnalysis;

namespace Muonroi.Pdf.Governance.Policies;

/// <summary>
/// Decorator that wraps any <see cref="IPdfCssPolicy"/> with an optional signature-verification
/// gate. When <see cref="PdfConfigs.RequirePolicySignature"/> is <c>true</c>, the provided
/// <paramref name="signatureVerifier"/> delegate is called before delegating to the inner policy;
/// a failing check throws <see cref="PdfPolicyException"/> with a
/// <c>gov.policy.signature-invalid</c> violation. When the signature check passes (or is
/// disabled), validation is fully delegated to the inner policy.
/// </summary>
/// <param name="inner">The inner policy to delegate CSS validation to after the signature check.</param>
/// <param name="configs">PDF configuration that controls whether signature verification is required.</param>
/// <param name="signatureVerifier">
/// Delegate that returns <c>true</c> when the policy configuration signature is valid.
/// Invoked only when <see cref="PdfConfigs.RequirePolicySignature"/> is <c>true</c>.
/// </param>
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

    /// <summary>Gets the policy identifier of the wrapped inner policy.</summary>
    public string Id => _inner.Id;

    /// <summary>Gets the structural limits enforced by the wrapped inner policy.</summary>
    public PdfPolicyLimits Limits => _inner.Limits;

    /// <summary>
    /// Verifies the policy configuration signature (when required) and then delegates
    /// CSS validation to the wrapped inner policy.
    /// </summary>
    /// <param name="documentContext">The styled document context to validate.</param>
    /// <param name="ct">Cancellation token forwarded to the inner policy's <c>ValidateAsync</c>.</param>
    /// <returns>The <see cref="PolicyValidationResult"/> produced by the inner policy.</returns>
    /// <exception cref="PdfPolicyException">
    /// Thrown when <see cref="PdfConfigs.RequirePolicySignature"/> is <c>true</c> and
    /// the configured signature verifier returns <c>false</c>.
    /// </exception>
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
