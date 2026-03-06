namespace Muonroi.RuleEngine.Runtime.Rules;

public interface IRuleSetDefinitionValidator
{
    RuleSetValidationResult Validate(string workflowName, string json);
}
