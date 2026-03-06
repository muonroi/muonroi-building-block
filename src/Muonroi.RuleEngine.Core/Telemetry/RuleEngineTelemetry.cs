namespace Muonroi.RuleEngine.Core.Telemetry;

/// <summary>
/// Provides OpenTelemetry sources and instruments for the rule engine.
/// </summary>
internal static class RuleEngineTelemetry
{
    /// <summary>Activity source used to create spans for rule evaluation.</summary>
    public static readonly ActivitySource ActivitySource = new("Muonroi.RuleEngine");

    /// <summary>Meter used to publish rule engine metrics.</summary>
    public static readonly Meter Meter = new("Muonroi.RuleEngine");

    /// <summary>Counter tracking how many rules have been matched.</summary>
    public static readonly Counter<long> RulesMatched = Meter.CreateCounter<long>("rules.matched");

    /// <summary>Counter tracking how many rules have been fired.</summary>
    public static readonly Counter<long> RulesFired = Meter.CreateCounter<long>("rules.fired");

    /// <summary>Histogram capturing rule evaluation duration in milliseconds.</summary>
    public static readonly Histogram<double> RuleEvalDuration = Meter.CreateHistogram<double>(
        "rule.eval.duration",
        unit: "ms");
}
