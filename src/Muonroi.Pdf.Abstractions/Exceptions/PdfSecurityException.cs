namespace Muonroi.Pdf.Abstractions.Exceptions;

/// <summary>
/// Thrown when a security constraint is violated during PDF generation, such as an attempt
/// to load an external resource from a disallowed origin or access a restricted file path.
/// </summary>
/// <param name="ruleId">Security rule identifier that was violated (e.g. <c>"SEC-01"</c>).</param>
/// <param name="detail">Human-readable description of the specific security violation.</param>
public sealed class PdfSecurityException(string ruleId, string detail) : PdfException(ruleId, detail, $"PDF security violation [{ruleId}]: {detail}")
{
}
