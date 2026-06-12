using FluentAssertions;
using Muonroi.RuleEngine.Runtime.Adapters;
using Xunit;

namespace Muonroi.RuleEngine.Runtime.Tests;

public sealed class RuleGraphEntryTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        RuleGraphEntry entry = new();

        entry.NodeId.Should().BeEmpty();
        entry.NodeType.Should().BeEmpty();
        entry.RuleCode.Should().BeNull();
        entry.Order.Should().Be(0);
        entry.DependsOn.Should().BeEmpty();
        entry.ExpressionLanguage.Should().Be("feel");
        entry.FeelExpression.Should().BeNull();
        entry.JavaScriptExpression.Should().BeNull();
        entry.OutputFields.Should().BeEmpty();
        entry.LiquidTemplate.Should().BeNull();
        entry.LiquidOutputFormat.Should().Be("text");
        entry.LiquidOutputKey.Should().BeNull();
        entry.DecisionTableId.Should().BeNull();
        entry.DecisionTableCode.Should().BeNull();
        entry.FailOnNoMatch.Should().BeTrue();
        entry.SubFlowCode.Should().BeNull();
        entry.InputMappings.Should().BeEmpty();
        entry.OutputMappings.Should().BeEmpty();
        entry.ConnectorType.Should().BeNull();
        entry.ConnectorConfig.Should().BeNull();
        entry.CredentialId.Should().BeNull();
        entry.ResilienceOptions.Should().BeNull();
        entry.IncomingEdges.Should().BeEmpty();
        entry.HasAlwaysEdge.Should().BeFalse();
        entry.HasOnFalseEdge.Should().BeFalse();
        entry.HasOnErrorEdge.Should().BeFalse();
    }

    [Fact]
    public void CanSetAllProperties()
    {
        RuleGraphEntry entry = new()
        {
            NodeId = "n1",
            NodeType = "condition",
            RuleCode = "rule-1",
            Order = 5,
            DependsOn = ["dep-1"],
            ExpressionLanguage = "javascript",
            JavaScriptExpression = "x > 5",
            HasAlwaysEdge = true,
            HasOnFalseEdge = true,
            HasOnErrorEdge = true,
        };

        entry.NodeId.Should().Be("n1");
        entry.NodeType.Should().Be("condition");
        entry.RuleCode.Should().Be("rule-1");
        entry.Order.Should().Be(5);
        entry.ExpressionLanguage.Should().Be("javascript");
        entry.JavaScriptExpression.Should().Be("x > 5");
        entry.HasAlwaysEdge.Should().BeTrue();
        entry.HasOnFalseEdge.Should().BeTrue();
        entry.HasOnErrorEdge.Should().BeTrue();
    }
}

public sealed class FeelOutputFieldTests
{
    [Fact]
    public void DefaultValues()
    {
        FeelOutputField field = new();

        field.Path.Should().BeEmpty();
        field.ValueExpression.Should().BeEmpty();
        field.DataType.Should().Be("string");
    }
}

public sealed class SubFlowInputMappingTests
{
    [Fact]
    public void DefaultValues()
    {
        SubFlowInputMapping mapping = new();

        mapping.SourcePath.Should().BeEmpty();
        mapping.TargetPath.Should().BeEmpty();
        mapping.TransformExpression.Should().BeNull();
    }
}

public sealed class SubFlowOutputMappingTests
{
    [Fact]
    public void DefaultValues()
    {
        SubFlowOutputMapping mapping = new();

        mapping.ChildPath.Should().BeEmpty();
        mapping.ParentPath.Should().BeEmpty();
        mapping.ExposeToParent.Should().BeTrue();
    }
}

public sealed class ConnectorResilienceOptionsTests
{
    [Fact]
    public void DefaultValues()
    {
        ConnectorResilienceOptions opts = new();

        opts.RetryCount.Should().Be(3);
        opts.RetryDelaySeconds.Should().Be(1);
        opts.TimeoutSeconds.Should().Be(30);
        opts.CircuitBreakerThreshold.Should().Be(0.5);
    }
}

public sealed class RuleGraphIncomingEdgeTests
{
    [Fact]
    public void DefaultValues()
    {
        RuleGraphIncomingEdge edge = new();

        edge.SourceNodeId.Should().BeEmpty();
        edge.EdgeType.Should().Be("always");
    }
}
