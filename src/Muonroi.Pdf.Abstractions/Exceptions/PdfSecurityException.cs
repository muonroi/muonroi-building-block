namespace Muonroi.Pdf.Abstractions.Exceptions;

public sealed class PdfSecurityException : PdfException
{
    public PdfSecurityException(string ruleId, string detail)
        : base(ruleId, detail, $"PDF security violation [{ruleId}]: {detail}")
    {
    }
}
