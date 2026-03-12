using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Muonroi.RuleEngine.Core.Telemetry;

/// <summary>
/// Provides OpenTelemetry sources and instruments for the rule engine and mediator rule execution pipeline.
/// </summary>
public static class RuleEngineTelemetry
{
    /// <summary>
    /// Activity source used to create spans for rule evaluation.
    /// </summary>
    public static readonly ActivitySource ActivitySource = new("Muonroi.RuleEngine");

    /// <summary>
    /// Meter used to publish rule engine metrics.
    /// </summary>
    public static readonly Meter Meter = new("Muonroi.RuleEngine");

    /// <summary>
    /// Counter tracking how many rules have been matched.
    /// </summary>
    public static readonly Counter<long> RulesMatched = Meter.CreateCounter<long>("rules.matched");

    /// <summary>
    /// Counter tracking how many rules have been fired.
    /// </summary>
    public static readonly Counter<long> RulesFired = Meter.CreateCounter<long>("rules.fired");

    /// <summary>
    /// Histogram capturing rule evaluation duration in milliseconds.
    /// </summary>
    public static readonly Histogram<double> RuleEvalDuration = Meter.CreateHistogram<double>(
        "rule.eval.duration",
        unit: "ms");

    /// <summary>
    /// Counter tracking mediator rule execution outcomes.
    /// </summary>
    public static readonly Counter<long> RuleExecutionResult = Meter.CreateCounter<long>(
        "rule_execution_result",
        description: "Total mediator rule execution outcomes.");

    /// <summary>
    /// Histogram capturing mediator rule execution duration in milliseconds.
    /// </summary>
    public static readonly Histogram<long> RuleExecutionDurationMs = Meter.CreateHistogram<long>(
        "rule_eval_duration_ms",
        unit: "ms",
        description: "Mediator rule execution duration in milliseconds.");
}
