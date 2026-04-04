namespace Muonroi.Mediator.Behaviours;

/// <summary>
/// Structured payload attached to mediator rule execution log scopes.
/// </summary>
/// <param name="RuleCode">The unique rule code.</param>
/// <param name="HookPoint">The execution phase in which the rule ran.</param>
/// <param name="Passed">Indicates whether the rule passed.</param>
/// <param name="ElapsedMs">The total evaluation and execution duration in milliseconds.</param>
/// <param name="TenantId">The tenant identifier associated with the current execution context.</param>
internal sealed record RuleExecutionLogScope(
    string RuleCode,
    string HookPoint,
    bool Passed,
    long ElapsedMs,
    string? TenantId);
