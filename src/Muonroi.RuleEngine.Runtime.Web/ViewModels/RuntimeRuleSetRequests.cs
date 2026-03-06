namespace Muonroi.RuleEngine.Runtime.Web.ViewModels;

public sealed class SaveRuleSetRequest
{
    public JsonElement RuleSet { get; init; }
    public bool ActivateAfterSave { get; init; } = true;
    public string? Actor { get; init; }
    public string? Detail { get; init; }
}

public sealed class ActivateRuleSetRequest
{
    public string? Actor { get; init; }
    public string? Detail { get; init; }
}

public sealed class ValidateRuleSetRequest
{
    public JsonElement RuleSet { get; init; }
}

public sealed class DryRunRuleSetRequest
{
    public JsonElement RuleSet { get; init; }
    public JsonElement Context { get; init; }
    public string? ContextType { get; init; }
}
