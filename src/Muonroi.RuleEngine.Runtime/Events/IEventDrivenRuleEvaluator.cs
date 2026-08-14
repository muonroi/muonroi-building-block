namespace Muonroi.RuleEngine.Runtime.Events;

/// <summary>
/// Evaluates rules triggered by incoming CloudEvents.
/// </summary>
public interface IEventDrivenRuleEvaluator
{
    /// <summary>
    /// Handles an incoming CloudEvent by extracting the target workflow and input facts,
    /// then evaluating the rules against those inputs.
    /// </summary>
    /// <param name="cloudEvent">The incoming CloudEvent to process.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// An <see cref="EventEvaluationResult"/> describing the outcome.
    /// Returns <see cref="EventEvaluationStatus.Skipped"/> if the event cannot be processed.
    /// </returns>
    Task<EventEvaluationResult> HandleEventAsync(CloudEvent cloudEvent, CancellationToken ct = default);
}

/// <summary>
/// Result of evaluating rules triggered by an external CloudEvent.
/// </summary>
/// <param name="Status">The evaluation outcome status.</param>
/// <param name="OutputFacts">Output facts produced by the rule evaluation, if successful.</param>
/// <param name="ErrorMessage">Error message when <see cref="Status"/> is <see cref="EventEvaluationStatus.Failed"/>.</param>
public sealed record EventEvaluationResult(
    EventEvaluationStatus Status,
    IDictionary<string, object?>? OutputFacts = null,
    string? ErrorMessage = null);

/// <summary>
/// Possible outcomes from event-driven rule evaluation.
/// </summary>
public enum EventEvaluationStatus
{
    /// <summary>Rules were evaluated successfully.</summary>
    Success,

    /// <summary>Rule evaluation failed with an error.</summary>
    Failed,

    /// <summary>The event was not processed (e.g., missing required data).</summary>
    Skipped
}
