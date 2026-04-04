namespace Muonroi.RuleEngine.Proliferation.Brain;

/// <summary>
/// Recommendation for whether proliferation coverage can replace, supplement, or should not touch existing unit tests.
/// </summary>
public enum ReplacementRecommendation
{
    /// <summary>Proliferation scenarios fully cover the rule — unit tests can be safely removed.</summary>
    Replaceable = 0,

    /// <summary>Proliferation covers part of the rule — keep unit tests but add proliferation alongside.</summary>
    Supplement = 1,

    /// <summary>Insufficient coverage — keep existing unit tests unchanged.</summary>
    Keep = 2
}

/// <summary>
/// Full report of test replacement analysis for a workflow.
/// </summary>
public sealed record TestReplacementReport(
    string WorkflowName,
    RuleSetKind RuleType,
    double FieldCoveragePercent,
    double? FlowCoveragePercent,
    ReplacementRecommendation Recommendation,
    string RecommendationMessage,
    IReadOnlyList<string> UncoveredAreas);

/// <summary>
/// Maps (ruleType, fieldCoverage, flowCoverage) to a replacement recommendation
/// so developers know which unit tests can be safely retired once proliferation covers them.
/// </summary>
public static class TestReplacementAdvisor
{
    private const double InsufficientThreshold = 30.0;
    private const double ReplaceableThreshold = 80.0;
    private const double SupplementThreshold = 50.0;

    /// <summary>
    /// Produces a test replacement report for a given workflow and its coverage data.
    /// </summary>
    public static TestReplacementReport GetReport(
        string workflowName,
        RuleSetKind kind,
        CoverageReport fieldCoverage,
        FlowCoverageReport? flowCoverage)
    {
        double fieldPct = fieldCoverage.CoveragePercentage;
        double? flowPct = flowCoverage is not null
            ? (flowCoverage.NodeCoveragePercentage + flowCoverage.EdgeCoveragePercentage) / 2.0
            : null;

        // Collect uncovered areas from both field and flow coverage
        List<string> uncoveredAreas = [..fieldCoverage.UncoveredFields];
        if (flowCoverage is not null)
        {
            foreach (NodeCoverage node in flowCoverage.Nodes.Where(n => !n.IsCovered))
                uncoveredAreas.Add($"node:{node.NodeId}");
            foreach (EdgeCoverage edge in flowCoverage.Edges.Where(e => !e.IsCovered))
                uncoveredAreas.Add($"edge:{edge.EdgeId}");
        }

        // Global guard: any coverage below 30% is always Keep
        if (fieldPct < InsufficientThreshold)
        {
            return new TestReplacementReport(
                workflowName, kind, fieldPct, flowPct,
                ReplacementRecommendation.Keep,
                "Insufficient coverage — generate more scenarios before considering test replacement.",
                uncoveredAreas);
        }

        return kind switch
        {
            RuleSetKind.DecisionTable => EvaluateDecisionTable(workflowName, fieldPct, flowPct, uncoveredAreas, fieldCoverage.UncoveredFields),
            RuleSetKind.FlowGraph => EvaluateFlowGraph(workflowName, fieldPct, flowPct, flowCoverage, uncoveredAreas),
            RuleSetKind.Legacy => EvaluateLegacy(workflowName, fieldPct, flowPct, uncoveredAreas),
            RuleSetKind.CodeBased => EvaluateCodeBased(workflowName, fieldPct, flowPct, uncoveredAreas),
            _ => new TestReplacementReport(
                workflowName, kind, fieldPct, flowPct,
                ReplacementRecommendation.Keep,
                "Unknown rule type — keeping existing tests as a safety measure.",
                uncoveredAreas)
        };
    }

    private static TestReplacementReport EvaluateDecisionTable(
        string name, double fieldPct, double? flowPct,
        IReadOnlyList<string> uncoveredAreas, IReadOnlyList<string> uncoveredFields)
    {
        if (fieldPct >= ReplaceableThreshold)
            return new TestReplacementReport(name, RuleSetKind.DecisionTable, fieldPct, flowPct,
                ReplacementRecommendation.Replaceable,
                "Proliferation covers this decision table fully — unit tests for this rule can be retired.",
                uncoveredAreas);

        if (fieldPct >= SupplementThreshold)
        {
            string missing = uncoveredFields.Count > 0
                ? $"Uncovered fields: {string.Join(", ", uncoveredFields)}."
                : "Some rows may still lack coverage.";
            return new TestReplacementReport(name, RuleSetKind.DecisionTable, fieldPct, flowPct,
                ReplacementRecommendation.Supplement,
                $"Partial coverage — keep unit tests and add proliferation alongside. {missing}",
                uncoveredAreas);
        }

        return new TestReplacementReport(name, RuleSetKind.DecisionTable, fieldPct, flowPct,
            ReplacementRecommendation.Keep,
            "Coverage below threshold — keep all existing unit tests and generate more proliferation scenarios.",
            uncoveredAreas);
    }

    private static TestReplacementReport EvaluateFlowGraph(
        string name, double fieldPct, double? flowPct,
        FlowCoverageReport? flowCoverage, IReadOnlyList<string> uncoveredAreas)
    {
        // For flow graphs, require BOTH high field coverage AND high flow coverage to replace
        double effectiveFlowPct = flowPct ?? 0.0;

        if (fieldPct >= ReplaceableThreshold && effectiveFlowPct >= ReplaceableThreshold)
            return new TestReplacementReport(name, RuleSetKind.FlowGraph, fieldPct, flowPct,
                ReplacementRecommendation.Replaceable,
                "High field and flow coverage — proliferation scenarios cover the flow graph completely.",
                uncoveredAreas);

        if (fieldPct >= SupplementThreshold || effectiveFlowPct >= SupplementThreshold)
            return new TestReplacementReport(name, RuleSetKind.FlowGraph, fieldPct, flowPct,
                ReplacementRecommendation.Supplement,
                "Partial flow graph coverage — keep unit tests for uncovered paths and supplement with proliferation.",
                uncoveredAreas);

        return new TestReplacementReport(name, RuleSetKind.FlowGraph, fieldPct, flowPct,
            ReplacementRecommendation.Keep,
            "Insufficient flow graph coverage — keep existing unit tests unchanged.",
            uncoveredAreas);
    }

    private static TestReplacementReport EvaluateLegacy(
        string name, double fieldPct, double? flowPct, IReadOnlyList<string> uncoveredAreas)
    {
        // Legacy (Scriban/template-driven): AI-driven coverage can replace tests at 80%+
        if (fieldPct >= ReplaceableThreshold)
            return new TestReplacementReport(name, RuleSetKind.Legacy, fieldPct, flowPct,
                ReplacementRecommendation.Replaceable,
                "Proliferation covers this legacy/template rule well — unit tests can be retired.",
                uncoveredAreas);

        return new TestReplacementReport(name, RuleSetKind.Legacy, fieldPct, flowPct,
            ReplacementRecommendation.Supplement,
            "Partial legacy rule coverage — supplement existing tests with proliferation scenarios.",
            uncoveredAreas);
    }

    private static TestReplacementReport EvaluateCodeBased(
        string name, double fieldPct, double? flowPct, IReadOnlyList<string> uncoveredAreas)
    {
        // Code-based rules: proliferation supplements but never fully replaces (~60% cap per design decision)
        // Even at 90% field coverage, code logic may have branches that AI misses
        return new TestReplacementReport(name, RuleSetKind.CodeBased, fieldPct, flowPct,
            ReplacementRecommendation.Supplement,
            "Code-based rules contain explicit logic branches that require hand-written tests — proliferation supplements but does not replace them.",
            uncoveredAreas);
    }
}
