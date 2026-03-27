using FluentAssertions;
using Muonroi.RuleEngine.Proliferation.Brain;

namespace Muonroi.RuleEngine.Proliferation.Tests;

public class RuleSetKindDetectorTests
{
    [Fact]
    public void Detect_FlowGraph_WithFlowGraphProperty()
    {
        string json = """{"flowGraph": {"nodes": [], "edges": []}, "name": "test"}""";
        RuleSetKindDetector.Detect(json).Should().Be(RuleSetKind.FlowGraph);
    }

    [Fact]
    public void Detect_FlowGraph_WithNodesAndEdges()
    {
        string json = """{"nodes": [{"id": "1"}], "edges": [{"from": "1", "to": "2"}]}""";
        RuleSetKindDetector.Detect(json).Should().Be(RuleSetKind.FlowGraph);
    }

    [Fact]
    public void Detect_DecisionTable_WithHitPolicy()
    {
        string json = """{"hitPolicy": "First", "inputColumns": [], "outputColumns": [], "rules": []}""";
        RuleSetKindDetector.Detect(json).Should().Be(RuleSetKind.DecisionTable);
    }

    [Fact]
    public void Detect_DecisionTable_WithInputColumns()
    {
        string json = """{"inputColumns": [{"name": "amount"}], "outputColumns": [{"name": "result"}]}""";
        RuleSetKindDetector.Detect(json).Should().Be(RuleSetKind.DecisionTable);
    }

    [Fact]
    public void Detect_CodeBased_WithRuleCodeArray()
    {
        string json = """{"rules": [{"ruleCode": "RULE001", "condition": "amount > 0"}]}""";
        RuleSetKindDetector.Detect(json).Should().Be(RuleSetKind.CodeBased);
    }

    [Fact]
    public void Detect_Legacy_NonEmptyObject()
    {
        string json = """{"name": "old-rule", "version": 1}""";
        RuleSetKindDetector.Detect(json).Should().Be(RuleSetKind.Legacy);
    }

    [Fact]
    public void Detect_Unknown_EmptyObject()
    {
        RuleSetKindDetector.Detect("{}").Should().Be(RuleSetKind.Unknown);
    }

    [Fact]
    public void Detect_Unknown_EmptyString()
    {
        RuleSetKindDetector.Detect("").Should().Be(RuleSetKind.Unknown);
    }

    [Fact]
    public void Detect_Unknown_NullString()
    {
        RuleSetKindDetector.Detect(null!).Should().Be(RuleSetKind.Unknown);
    }

    [Fact]
    public void Detect_Unknown_MalformedJson()
    {
        RuleSetKindDetector.Detect("{invalid json").Should().Be(RuleSetKind.Unknown);
    }

    [Fact]
    public void Detect_RulesArrayWithoutRuleCode_Legacy()
    {
        string json = """{"rules": [{"name": "test"}], "version": 1}""";
        RuleSetKindDetector.Detect(json).Should().Be(RuleSetKind.Legacy);
    }

    [Fact]
    public void Detect_EmptyRulesArray_Legacy()
    {
        string json = """{"rules": [], "config": true}""";
        RuleSetKindDetector.Detect(json).Should().Be(RuleSetKind.Legacy);
    }
}
