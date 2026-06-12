namespace Muonroi.RuleGen.Models;

internal sealed record RuntimeRuleSet(
    string WorkflowName,
    int Version,
    string? TenantId,
    IReadOnlyList<RuntimeRuleDefinition> Rules);

internal sealed record RuntimeRuleDefinition(
    string Code,
    string Name,
    int Order,
    string HookPoint,
    IReadOnlyList<string> DependsOn,
    string? Condition,
    string? Action,
    string Type,
    string? Source);
