namespace Muonroi.RuleEngine.Core;

/// <summary>
/// Runtime options for routing execution between traditional code and rule orchestration.
/// </summary>
public sealed class MRuleEngineOptions
{
    public RuleExecutionMode ExecutionMode { get; set; } = RuleExecutionMode.Rules;
    public double TraditionalWeight { get; set; } = 0.5d;
    public double RulesWeight { get; set; } = 0.5d;
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
