using FluentAssertions;
using Muonroi.RuleEngine.Proliferation.Models;

namespace Muonroi.RuleEngine.Proliferation.Tests;

public class ProliferationSuggestionTests
{
    [Fact]
    public void ProliferationSuggestion_DefaultValues_AreCorrect()
    {
        ProliferationSuggestion suggestion = new()
        {
            WorkflowName = "test-workflow",
            FieldCoveragePercent = 0,
            Recommendation = "Critical"
        };

        suggestion.WorkflowName.Should().Be("test-workflow");
        suggestion.FieldCoveragePercent.Should().Be(0);
        suggestion.FlowNodeCoveragePercent.Should().BeNull();
        suggestion.FlowEdgeCoveragePercent.Should().BeNull();
        suggestion.UncoveredFields.Should().BeEmpty();
        suggestion.UncoveredNodes.Should().BeEmpty();
        suggestion.SuggestedFocusAreas.Should().BeEmpty();
        suggestion.TotalScenarios.Should().Be(0);
        suggestion.PassedScenarios.Should().Be(0);
        suggestion.FailedScenarios.Should().Be(0);
        suggestion.Recommendation.Should().Be("Critical");
    }

    [Fact]
    public void ProliferationSuggestion_WithData_ReturnsExpectedValues()
    {
        ProliferationSuggestion suggestion = new()
        {
            WorkflowName = "order-processing",
            FieldCoveragePercent = 75.5,
            FlowNodeCoveragePercent = 60.0,
            FlowEdgeCoveragePercent = 45.0,
            UncoveredFields = ["discountCode", "loyaltyPoints"],
            UncoveredNodes = ["validate-coupon", "apply-discount"],
            SuggestedFocusAreas = ["discountCode", "loyaltyPoints", "validate-coupon", "high-failure-rate"],
            TotalScenarios = 50,
            PassedScenarios = 38,
            FailedScenarios = 12,
            Recommendation = "Moderate"
        };

        suggestion.WorkflowName.Should().Be("order-processing");
        suggestion.FieldCoveragePercent.Should().Be(75.5);
        suggestion.FlowNodeCoveragePercent.Should().Be(60.0);
        suggestion.FlowEdgeCoveragePercent.Should().Be(45.0);
        suggestion.UncoveredFields.Should().HaveCount(2);
        suggestion.UncoveredNodes.Should().HaveCount(2);
        suggestion.SuggestedFocusAreas.Should().HaveCount(4);
        suggestion.SuggestedFocusAreas.Should().Contain("high-failure-rate");
        suggestion.TotalScenarios.Should().Be(50);
        suggestion.PassedScenarios.Should().Be(38);
        suggestion.FailedScenarios.Should().Be(12);
        suggestion.Recommendation.Should().Be("Moderate");
    }

    [Fact]
    public void TenantSimulationStats_PassRate_CalculatesCorrectly()
    {
        TenantSimulationStats stats = new()
        {
            TenantId = "tenant-1",
            TotalScenarios = 100,
            Passed = 80,
            Failed = 20
        };

        stats.PassRate.Should().Be(80.0);
    }

    [Fact]
    public void TenantSimulationStats_ZeroScenarios_PassRateIsZero()
    {
        TenantSimulationStats stats = new()
        {
            TenantId = "tenant-empty",
            TotalScenarios = 0,
            Passed = 0,
            Failed = 0
        };

        stats.PassRate.Should().Be(0.0);
    }
}
