namespace Muonroi.Pdf.Abstractions.Exceptions;

public abstract class PdfException : Exception
{
    public string RuleId { get; }
    public string Detail { get; }

    protected PdfException(string ruleId, string detail, string message, Exception? inner = null)
        : base(message, inner)
    {
        RuleId = ruleId;
        Detail = detail;
    }
}
