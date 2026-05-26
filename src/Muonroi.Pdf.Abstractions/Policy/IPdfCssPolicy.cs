namespace Muonroi.Pdf.Abstractions.Policy;

/// <summary>
/// Defines the HTML/CSS subset accepted by the rendering pipeline.
/// Enforced after parsing and cascade resolution, before layout begins.
/// </summary>
/// <remarks>
/// A policy implementation MUST be side-effect free and thread-safe.
/// The default policy <c>DefaultStrictPolicy</c> ships in <c>Muonroi.Pdf.Governance</c>.
/// </remarks>
public interface IPdfCssPolicy
{
    /// <summary>
    /// Policy identifier (stable across versions). Used in telemetry and audit logs.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Hard limits enforced before parsing begins.
    /// </summary>
    PdfPolicyLimits Limits { get; }

    /// <summary>
    /// Validates a parsed-and-cascaded document against the policy.
    /// </summary>
    /// <param name="documentContext">Opaque document context produced by the parser/cascade stage.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result describing accepted, rejected, or warning conditions.</returns>
    ValueTask<PolicyValidationResult> ValidateAsync(IPdfDocumentContext documentContext, CancellationToken cancellationToken = default);
}

/// <summary>
/// Opaque context handed to policies. Engine implementations expose the parsed DOM and computed styles here.
/// </summary>
public interface IPdfDocumentContext
{
    /// <summary>Total element count after parsing.</summary>
    int ElementCount { get; }

    /// <summary>Maximum DOM nesting depth.</summary>
    int MaxDepth { get; }

    /// <summary>Resolved stylesheet byte size (sum of all stylesheets).</summary>
    long TotalStylesheetBytes { get; }

    /// <summary>Source HTML byte size.</summary>
    long SourceHtmlBytes { get; }
}
