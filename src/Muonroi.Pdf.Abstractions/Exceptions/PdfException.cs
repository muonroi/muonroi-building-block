namespace Muonroi.Pdf.Abstractions.Exceptions;

public abstract class PdfException(string ruleId, string detail, string message, Exception? inner = null) : Exception(message, inner)
{
    public string RuleId { get; } = ruleId;
    public string Detail { get; } = detail;
}
