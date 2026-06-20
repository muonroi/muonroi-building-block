namespace Muonroi.Pdf.Abstractions.Exceptions;

/// <summary>
/// Base class for all PDF pipeline exceptions. Carries a structured rule identifier
/// and a human-readable detail message in addition to the standard exception message.
/// </summary>
/// <param name="ruleId">Policy or validation rule identifier that triggered this exception (e.g. <c>"G02"</c>).</param>
/// <param name="detail">Human-readable description of what specifically violated the rule.</param>
/// <param name="message">Exception message surfaced by <see cref="Exception.Message"/>.</param>
/// <param name="inner">Optional inner exception that caused this failure.</param>
public abstract class PdfException(string ruleId, string detail, string message, Exception? inner = null) : Exception(message, inner)
{
    /// <summary>Structured rule identifier that classifies the violation (e.g. <c>"G02"</c>, <c>"SEC-01"</c>).</summary>
    public string RuleId { get; } = ruleId;

    /// <summary>Human-readable detail describing the specific condition that triggered this exception.</summary>
    public string Detail { get; } = detail;
}
