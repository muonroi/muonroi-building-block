namespace Muonroi.RuleEngine.Runtime.Rules;

/// <summary>
/// Represents the result of validating a ruleset payload.
/// </summary>
public sealed class RuleSetValidationResult
{
    /// <summary>Gets a value indicating whether validation passed.</summary>
    public bool IsValid => Errors.Count == 0;

    /// <summary>Gets or sets the workflow name.</summary>
    public string WorkflowName { get; set; } = string.Empty;

    /// <summary>Gets or sets the detected payload shape.</summary>
    public string Shape { get; set; } = "Unknown";

    /// <summary>Gets the validation errors.</summary>
    public List<RuleSetValidationIssue> Errors { get; } = [];

    /// <summary>Gets the validation warnings.</summary>
    public List<RuleSetValidationIssue> Warnings { get; } = [];

    /// <summary>
    /// Throws when the result contains validation errors.
    /// </summary>
    public void ThrowIfInvalid()
    {
        if (IsValid)
        {
            return;
        }

        string message = string.Join("; ", Errors.Select(x => x.Message));
        MGuard.Fail<object>(message);
    }
}
