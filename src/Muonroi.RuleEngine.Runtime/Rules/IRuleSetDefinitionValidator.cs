namespace Muonroi.RuleEngine.Runtime.Rules;

/// <summary>
/// Validates ruleset definitions before persistence.
/// </summary>
public interface IRuleSetDefinitionValidator
{
    /// <summary>Validates a ruleset definition.</summary>
    /// <param name="workflowName">Workflow name.</param>
    /// <param name="json">Ruleset JSON.</param>
    /// <returns>Validation result.</returns>
    RuleSetValidationResult Validate(string workflowName, string json);
}
