namespace Muonroi.Pdf.Abstractions.Exceptions;

/// <summary>
/// Thrown when an input resource exceeds a configured safety limit enforced by
/// <c>TokenBudgetEnforcer</c> (e.g. HTML byte size, DOM depth, element count, image pixels).
/// </summary>
/// <param name="ruleId">Policy rule identifier that defines the exceeded limit.</param>
/// <param name="limitName">Human-readable name of the limit that was breached (e.g. <c>"MaxHtmlBytes"</c>).</param>
/// <param name="actualValue">The actual measured value that exceeded the limit.</param>
/// <param name="limitValue">The configured maximum value that was exceeded.</param>
public sealed class PdfInputLimitException(string ruleId, string limitName, long actualValue, long limitValue) : PdfException(
        ruleId,
        $"PDF input limit exceeded: {limitName} = {actualValue}, limit = {limitValue} (rule: {ruleId})",
        $"PDF input limit exceeded: {limitName} = {actualValue}, limit = {limitValue} (rule: {ruleId})")
{
    /// <summary>Human-readable name of the limit that was breached (e.g. <c>"MaxHtmlBytes"</c>, <c>"MaxDomDepth"</c>).</summary>
    public string LimitName { get; } = limitName;

    /// <summary>The actual measured value that caused the limit to be exceeded.</summary>
    public long ActualValue { get; } = actualValue;

    /// <summary>The configured upper bound that the actual value exceeded.</summary>
    public long LimitValue { get; } = limitValue;
}
