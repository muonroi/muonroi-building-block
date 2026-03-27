namespace Muonroi.RuleEngine.Proliferation.Brain;

/// <summary>
/// Coverage-weighted budget allocator that assigns more scenarios to under-tested rules.
/// Coverage tiers:
///   &lt; 30%  → 3× base budget
///   &lt; 60%  → 2× base budget
///   &lt; 80%  → 1× base budget
///   ≥ 80%  → 0.5× base budget (diminishing returns)
/// Failure bonus:
///   &gt; 30% failure rate → 1.5× multiplier
///   &gt; 10% failure rate → 1.2× multiplier
///   ≤ 10% failure rate → no bonus
/// </summary>
public sealed class CoverageWeightedBudgetAllocator : IBudgetAllocator
{
    /// <summary>Allocates budget based on coverage and failure rate.</summary>
    public int AllocateBudget(string seedRuleCode, double coveragePercent, double failureRate, int baseBudget)
    {
        if (baseBudget <= 0) baseBudget = 1;

        // Coverage multiplier (inversely proportional to coverage)
        double coverageMultiplier = coveragePercent switch
        {
            < 30 => 3.0,
            < 60 => 2.0,
            < 80 => 1.0,
            _ => 0.5
        };

        // Failure rate bonus (high failure → more budget to probe)
        double failureBonus = failureRate switch
        {
            > 30 => 1.5,
            > 10 => 1.2,
            _ => 1.0
        };

        int allocated = (int)Math.Ceiling(baseBudget * coverageMultiplier * failureBonus);

        // Guarantee at least 1 scenario is always allocated
        return Math.Max(1, allocated);
    }
}
