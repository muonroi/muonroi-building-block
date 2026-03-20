namespace Muonroi.RuleEngine.Core;

/// <summary>
/// Runtime options for routing execution between traditional code and rule orchestration.
/// </summary>
public sealed class MRuleEngineOptions
{
    /// <summary>
    /// Gets or sets the execution mode.
    /// </summary>
    public RuleExecutionMode ExecutionMode { get; set; } = RuleExecutionMode.Rules;

    /// <summary>
    /// Gets or sets the weight for traditional execution in A/B testing.
    /// </summary>
    public double TraditionalWeight { get; set; } = 0.5d;

    /// <summary>
    /// Gets or sets the weight for rule engine execution in A/B testing.
    /// </summary>
    public double RulesWeight { get; set; } = 0.5d;

    /// <summary>
    /// Gets or sets a value indicating whether to log differences between traditional and rule engine execution.
    /// </summary>
    public bool LogDifferences { get; set; } = true;

    internal double NormalizedTraditionalWeight
    {
        get
        {
            double total = TraditionalWeight + RulesWeight;
            if (total <= 0d)
            {
                return 0.5d;
            }

            return TraditionalWeight / total;
        }
    }
}
