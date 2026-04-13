using FluentAssertions;
using Muonroi.RuleEngine.Proliferation.Brain;

namespace Muonroi.RuleEngine.Proliferation.Tests;

public class PromptBuilderRuleKindTests
{
    private readonly DefaultPromptBuilder _builder = new();

    [Fact]
    public void BuildSystemPrompt_Unknown_ReturnsGenericPrompt()
    {
        string prompt = _builder.BuildSystemPrompt(RuleSetKind.Unknown);
        string generic = _builder.BuildSystemPrompt();

        prompt.Should().Be(generic);
    }

    [Fact]
    public void BuildSystemPrompt_FlowGraph_ContainsPathCoverage()
    {
        string prompt = _builder.BuildSystemPrompt(RuleSetKind.FlowGraph);

        prompt.Should().Contain("PATH COVERAGE");
        prompt.Should().Contain("UNREACHABLE NODES");
        prompt.Should().Contain("ERROR EDGES");
        prompt.Should().Contain("GATEWAY LOGIC");
        prompt.Should().Contain("Flow Graph");
    }

    [Fact]
    public void BuildSystemPrompt_DecisionTable_ContainsRowCoverage()
    {
        string prompt = _builder.BuildSystemPrompt(RuleSetKind.DecisionTable);

        prompt.Should().Contain("ROW COVERAGE");
        prompt.Should().Contain("GAP ANALYSIS");
        prompt.Should().Contain("HIT POLICY");
        prompt.Should().Contain("BOUNDARY VALUES");
        prompt.Should().Contain("Decision Table");
    }

    [Fact]
    public void BuildSystemPrompt_CodeBased_ContainsBooleanBoundaries()
    {
        string prompt = _builder.BuildSystemPrompt(RuleSetKind.CodeBased);

        prompt.Should().Contain("BOOLEAN BOUNDARIES");
        prompt.Should().Contain("NULL INPUTS");
        prompt.Should().Contain("TYPE COERCION");
        prompt.Should().Contain("RULE ORDERING");
        prompt.Should().Contain("Code-Based");
    }

    [Fact]
    public void BuildSystemPrompt_Legacy_ReturnsGenericPrompt()
    {
        string prompt = _builder.BuildSystemPrompt(RuleSetKind.Legacy);
        string generic = _builder.BuildSystemPrompt();

        prompt.Should().Be(generic);
    }

    [Fact]
    public void BuildSystemPrompt_AllKinds_IncludeGenericBase()
    {
        string generic = _builder.BuildSystemPrompt();

        foreach (RuleSetKind kind in Enum.GetValues<RuleSetKind>())
        {
            string prompt = _builder.BuildSystemPrompt(kind);
            prompt.Should().Contain("rule proliferation analyzer", because: $"kind={kind} should contain generic base");
        }
    }
}
