using Muonroi.Pdf.Abstractions.Policy;

namespace Muonroi.Pdf.Abstractions;

/// <summary>
/// Result of a successful render. The <see cref="Stream"/> overload of
/// <see cref="IMPdfService"/> writes directly to a caller-owned stream and returns this
/// metadata without buffering the full document.
/// </summary>
public sealed record PdfRenderResult(
    int PageCount,
    long ByteCount,
    TimeSpan Elapsed,
    string TemplateHash,
    string PolicyId,
    IReadOnlyList<PolicyViolation> Diagnostics);
