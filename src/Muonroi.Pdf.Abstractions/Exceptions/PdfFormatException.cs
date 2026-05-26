namespace Muonroi.Pdf.Abstractions.Exceptions;

public sealed class PdfFormatException : PdfException
{
    public PdfFormatException(string ruleId, string message, Exception? inner = null)
        : base(ruleId, message, message, inner)
    {
    }
}
