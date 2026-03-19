namespace Muonroi.Mediator.Exceptions;

/// <summary>
/// Thrown when pre-handler rule engine validation fails.
/// </summary>
public sealed class MRuleViolationException : Exception
{
    public IReadOnlyList<string> Errors { get; }
    public string RuleCode { get; }

    public MRuleViolationException(string ruleCode, IReadOnlyList<string> errors)
        : base($"Rule '{ruleCode}' failed: {string.Join("; ", errors)}")
    {
        RuleCode = ruleCode;
        Errors = errors;
    }
}
