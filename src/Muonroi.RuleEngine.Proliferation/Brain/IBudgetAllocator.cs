namespace Muonroi.RuleEngine.Proliferation.Brain;

/// <summary>
/// Allocates scenario generation budget based on coverage and failure metrics.
/// Higher budget is assigned to under-tested rules to prioritize coverage gaps.
/// </summary>
public interface IBudgetAllocator
{
    /// <summary>
    /// Calculates the scenario budget for a specific rule.
    /// </summary>
    /// <param name="seedRuleCode">The rule/workflow identifier.</param>
    /// <param name="coveragePercent">Current field coverage percentage (0-100).</param>
    /// <param name="failureRate">Failure rate percentage (0-100). 0 means no failures yet.</param>
    /// <param name="baseBudget">Base budget to multiply (typically MaxScenariosPerRule).</param>
    /// <returns>Allocated budget (>= 1).</returns>
    int AllocateBudget(string seedRuleCode, double coveragePercent, double failureRate, int baseBudget);
}
