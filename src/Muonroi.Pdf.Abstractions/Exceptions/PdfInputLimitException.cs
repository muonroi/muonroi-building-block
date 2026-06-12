namespace Muonroi.Pdf.Abstractions.Exceptions;

public sealed class PdfInputLimitException : PdfException
{
    public string LimitName { get; }
    public long ActualValue { get; }
    public long LimitValue { get; }

    public PdfInputLimitException(string ruleId, string limitName, long actualValue, long limitValue)
        : base(
            ruleId,
            $"PDF input limit exceeded: {limitName} = {actualValue}, limit = {limitValue} (rule: {ruleId})",
            $"PDF input limit exceeded: {limitName} = {actualValue}, limit = {limitValue} (rule: {ruleId})")
    {
        LimitName = limitName;
        ActualValue = actualValue;
        LimitValue = limitValue;
    }
}
