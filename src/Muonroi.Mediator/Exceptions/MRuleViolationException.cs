using Muonroi.Core.Abstractions.Exceptions;

namespace Muonroi.Mediator.Exceptions;

/// <summary>
/// Thrown when pre-handler rule engine validation fails.
/// </summary>
public sealed class MRuleViolationException : MException
{
    /// <summary>
    /// Gets the Errors.
    /// </summary>
    public IReadOnlyList<string> Errors { get; }
    /// <summary>
    /// Gets the Rule Code.
    /// </summary>
    public string RuleCode { get; }

    /// <summary>
    /// Initializes a new instance of MRuleViolationException.
    /// </summary>
    public MRuleViolationException(string ruleCode, IReadOnlyList<string> errors)
        : base("RULE_VIOLATION", $"Rule '{ruleCode}' failed: {string.Join("; ", errors)}", MExceptionCategory.Domain, 422)
    {
        RuleCode = ruleCode;
        Errors = errors;
        Details["RuleCode"] = ruleCode;
        Details["Errors"] = errors;
    }
}
