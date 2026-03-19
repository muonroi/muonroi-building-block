using FluentAssertions;
using Muonroi.RuleEngine.Proliferation.Brain;

namespace Muonroi.RuleEngine.Proliferation.Tests;

public class TestReplacementAdvisorTests
{
    // ──────────────────────────────────────────────
    // Helper: create coverage reports
    // ──────────────────────────────────────────────

    private static CoverageReport Coverage(double pct, params string[] uncovered)
    {
        List<string> all = ["field1", "field2", "field3"];
        List<string> coveredFields = all.Except(uncovered).ToList();
        return new CoverageReport(coveredFields, uncovered.ToList(), pct);
    }

    // ──────────────────────────────────────────────
    // DecisionTable
    // ──────────────────────────────────────────────

    [Fact]
    public void GetReport_DecisionTable_90Percent_IsReplaceable()
    {
        CoverageReport fieldCoverage = Coverage(90.0);

        TestReplacementReport report = TestReplacementAdvisor.GetReport(
            "order-eligibility", RuleSetKind.DecisionTable, fieldCoverage, null);

        report.Recommendation.Should().Be(ReplacementRecommendation.Replaceable);
        report.RecommendationMessage.Should().NotBeNullOrEmpty();
        report.WorkflowName.Should().Be("order-eligibility");
        report.RuleType.Should().Be(RuleSetKind.DecisionTable);
    }

    [Fact]
    public void GetReport_DecisionTable_50Percent_IsSupplement()
    {
        CoverageReport fieldCoverage = Coverage(50.0, "field3");

        TestReplacementReport report = TestReplacementAdvisor.GetReport(
            "pricing-rules", RuleSetKind.DecisionTable, fieldCoverage, null);

        report.Recommendation.Should().Be(ReplacementRecommendation.Supplement);
        report.RecommendationMessage.Should().Contain("field3");
        report.UncoveredAreas.Should().Contain("field3");
    }

    [Fact]
    public void GetReport_DecisionTable_20Percent_IsKeep()
    {
        CoverageReport fieldCoverage = Coverage(20.0, "field1", "field2", "field3");

        TestReplacementReport report = TestReplacementAdvisor.GetReport(
            "risk-check", RuleSetKind.DecisionTable, fieldCoverage, null);

        report.Recommendation.Should().Be(ReplacementRecommendation.Keep);
        report.RecommendationMessage.Should().NotBeNullOrEmpty();
    }

    // ──────────────────────────────────────────────
    // FlowGraph
    // ──────────────────────────────────────────────

    [Fact]
    public void GetReport_FlowGraph_HighFieldAndFlowCoverage_IsReplaceable()
    {
        CoverageReport fieldCoverage = Coverage(85.0);
        FlowCoverageReport flowCoverage = new(
            [new NodeCoverage("n1", "Start", true), new NodeCoverage("n2", "End", true)],
            [],
            90.0, // NodeCoveragePercentage
            90.0  // EdgeCoveragePercentage
        );

        TestReplacementReport report = TestReplacementAdvisor.GetReport(
            "workflow-approval", RuleSetKind.FlowGraph, fieldCoverage, flowCoverage);

        report.Recommendation.Should().Be(ReplacementRecommendation.Replaceable);
    }

    [Fact]
    public void GetReport_FlowGraph_LowFlowCoverage_IsSupplement()
    {
        CoverageReport fieldCoverage = Coverage(90.0);
        FlowCoverageReport flowCoverage = new(
            [new NodeCoverage("n1", "Start", false), new NodeCoverage("n2", "End", true)],
            [],
            50.0,
            50.0
        );

        TestReplacementReport report = TestReplacementAdvisor.GetReport(
            "claim-processor", RuleSetKind.FlowGraph, fieldCoverage, flowCoverage);

        report.Recommendation.Should().Be(ReplacementRecommendation.Supplement);
    }

    // ──────────────────────────────────────────────
    // CodeBased
    // ──────────────────────────────────────────────

    [Fact]
    public void GetReport_CodeBased_90Percent_IsSupplement()
    {
        CoverageReport fieldCoverage = Coverage(90.0);

        TestReplacementReport report = TestReplacementAdvisor.GetReport(
            "payment-validation", RuleSetKind.CodeBased, fieldCoverage, null);

        // Code-based always supplements per user decision (cap ~60%)
        report.Recommendation.Should().Be(ReplacementRecommendation.Supplement);
    }

    [Fact]
    public void GetReport_CodeBased_20Percent_IsKeep()
    {
        CoverageReport fieldCoverage = Coverage(20.0, "field1", "field2");

        TestReplacementReport report = TestReplacementAdvisor.GetReport(
            "credit-score", RuleSetKind.CodeBased, fieldCoverage, null);

        report.Recommendation.Should().Be(ReplacementRecommendation.Keep);
    }

    // ──────────────────────────────────────────────
    // Unknown kind
    // ──────────────────────────────────────────────

    [Fact]
    public void GetReport_Unknown_AnyPercentage_IsKeep()
    {
        CoverageReport fieldCoverage = Coverage(95.0);

        TestReplacementReport report = TestReplacementAdvisor.GetReport(
            "unknown-rule", RuleSetKind.Unknown, fieldCoverage, null);

        report.Recommendation.Should().Be(ReplacementRecommendation.Keep);
        report.RecommendationMessage.Should().NotBeNullOrEmpty();
    }

    // ──────────────────────────────────────────────
    // Legacy kind
    // ──────────────────────────────────────────────

    [Fact]
    public void GetReport_Legacy_80Percent_IsReplaceable()
    {
        CoverageReport fieldCoverage = Coverage(80.0);

        TestReplacementReport report = TestReplacementAdvisor.GetReport(
            "legacy-template", RuleSetKind.Legacy, fieldCoverage, null);

        report.Recommendation.Should().Be(ReplacementRecommendation.Replaceable);
    }

    [Fact]
    public void GetReport_Legacy_60Percent_IsSupplement()
    {
        CoverageReport fieldCoverage = Coverage(60.0, "field2");

        TestReplacementReport report = TestReplacementAdvisor.GetReport(
            "legacy-template-v2", RuleSetKind.Legacy, fieldCoverage, null);

        report.Recommendation.Should().Be(ReplacementRecommendation.Supplement);
    }

    // ──────────────────────────────────────────────
    // Coverage below 30% always Keep
    // ──────────────────────────────────────────────

    [Fact]
    public void GetReport_AnyKind_Below30Percent_IsKeepWithInsufficientMessage()
    {
        foreach (RuleSetKind kind in Enum.GetValues<RuleSetKind>())
        {
            CoverageReport fieldCoverage = Coverage(25.0, "field1", "field2");

            TestReplacementReport report = TestReplacementAdvisor.GetReport(
                "test-rule", kind, fieldCoverage, null);

            report.Recommendation.Should().Be(ReplacementRecommendation.Keep,
                because: $"coverage < 30% should always be Keep for {kind}");
            report.RecommendationMessage.Should().Contain("insufficient",
                because: $"message should mention insufficient coverage for {kind}",
                StringComparison.OrdinalIgnoreCase);
        }
    }

    // ──────────────────────────────────────────────
    // Report structure
    // ──────────────────────────────────────────────

    [Fact]
    public void GetReport_Always_PopulatesAllFields()
    {
        CoverageReport fieldCoverage = Coverage(70.0, "field2");

        TestReplacementReport report = TestReplacementAdvisor.GetReport(
            "my-workflow", RuleSetKind.DecisionTable, fieldCoverage, null);

        report.WorkflowName.Should().Be("my-workflow");
        report.RuleType.Should().Be(RuleSetKind.DecisionTable);
        report.FieldCoveragePercent.Should().Be(70.0);
        report.FlowCoveragePercent.Should().BeNull();
        report.Recommendation.Should().NotBe(default);
        report.RecommendationMessage.Should().NotBeNullOrEmpty();
    }
}
