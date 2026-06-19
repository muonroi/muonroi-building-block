namespace Muonroi.Pdf.Abstractions.Exceptions;

public sealed class PdfInputLimitException(string ruleId, string limitName, long actualValue, long limitValue) : PdfException(
        ruleId,
        $"PDF input limit exceeded: {limitName} = {actualValue}, limit = {limitValue} (rule: {ruleId})",
        $"PDF input limit exceeded: {limitName} = {actualValue}, limit = {limitValue} (rule: {ruleId})")
{
    public string LimitName { get; } = limitName;
    public long ActualValue { get; } = actualValue;
    public long LimitValue { get; } = limitValue;
}
