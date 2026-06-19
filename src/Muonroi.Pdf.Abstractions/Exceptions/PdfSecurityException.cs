namespace Muonroi.Pdf.Abstractions.Exceptions;

public sealed class PdfSecurityException(string ruleId, string detail) : PdfException(ruleId, detail, $"PDF security violation [{ruleId}]: {detail}")
{
}
