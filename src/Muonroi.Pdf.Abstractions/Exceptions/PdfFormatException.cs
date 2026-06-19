namespace Muonroi.Pdf.Abstractions.Exceptions;

public sealed class PdfFormatException(string ruleId, string message, Exception? inner = null) : PdfException(ruleId, message, message, inner)
{
}
