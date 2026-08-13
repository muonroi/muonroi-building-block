namespace Muonroi.RuleEngine.Abstractions;

/// <summary>
/// Summary of a rule orchestration run, including outcomes and any compensation errors.
/// </summary>
/// <param name="IsSuccess">
/// Indicates whether the orchestrator completed successfully under the chosen execution mode.
/// </param>
/// <param name="ExecutionMode">The execution mode used for the run.</param>
/// <param name="Facts">The fact bag produced during execution.</param>
/// <param name="RuleResults">Per-rule evaluation results keyed by rule code.</param>
/// <param name="Errors">Errors produced while executing rules or orchestrator infrastructure.</param>
/// <param name="CompensationErrors">Errors reported during compensation attempts.</param>
public sealed record OrchestratorResult(
    bool IsSuccess,
    ExecutionMode ExecutionMode,
    FactBag Facts,
    IReadOnlyDictionary<string, RuleResult> RuleResults,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> CompensationErrors)
{
    /// <summary>
    /// Creates a successful result.
    /// </summary>
    /// <param name="executionMode">The execution mode used for the run.</param>
    /// <param name="facts">The fact bag produced during execution.</param>
    /// <param name="ruleResults">Per-rule evaluation results keyed by rule code.</param>
    /// <returns>A successful <see cref="OrchestratorResult"/>.</returns>
    public static OrchestratorResult Success(
        ExecutionMode executionMode,
        FactBag facts,
        IReadOnlyDictionary<string, RuleResult> ruleResults)
    {
        return new OrchestratorResult(true, executionMode, facts, ruleResults, [], []);
    }

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    /// <param name="executionMode">The execution mode used for the run.</param>
    /// <param name="facts">The fact bag produced during execution.</param>
    /// <param name="ruleResults">Per-rule evaluation results keyed by rule code.</param>
    /// <param name="errors">Errors produced while executing rules or orchestrator infrastructure.</param>
    /// <param name="compensationErrors">Errors reported during compensation attempts.</param>
    /// <returns>A failed <see cref="OrchestratorResult"/>.</returns>
    public static OrchestratorResult Failure(
        ExecutionMode executionMode,
        FactBag facts,
        IReadOnlyDictionary<string, RuleResult> ruleResults,
        IReadOnlyList<string> errors,
        IReadOnlyList<string> compensationErrors)
    {
        return new OrchestratorResult(false, executionMode, facts, ruleResults, errors, compensationErrors);
    }
}
