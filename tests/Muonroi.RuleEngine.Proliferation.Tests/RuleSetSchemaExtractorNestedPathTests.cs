using FluentAssertions;
using Muonroi.RuleEngine.Proliferation.Brain;

namespace Muonroi.RuleEngine.Proliferation.Tests;

/// <summary>
/// Tests for nested path regex fix: input.user.verified → "user.verified" (not just "user").
/// </summary>
public class RuleSetSchemaExtractorNestedPathTests
{
    [Fact]
    public void Extract_FlowGraph_NestedTwoLevel_CapturesDottedPath()
    {
        // Arrange: FEEL expression with input.user.verified
        string ruleSetJson = """
        {
          "nodes": [
            {
              "data": {
                "condition": "input.user.verified == true"
              }
            }
          ]
        }
        """;

        // Act
        RuleSetSchema schema = RuleSetSchemaExtractor.Extract(ruleSetJson, RuleSetKind.FlowGraph);

        // Assert: should capture "user.verified" not just "user"
        schema.InputFields.Should().Contain(f => f.Name == "user.verified",
            "nested path input.user.verified should be extracted as 'user.verified'");
    }

    [Fact]
    public void Extract_FlowGraph_NestedThreeLevel_CapturesDottedPath()
    {
        // Arrange: FEEL expression with input.order.items.count
        string ruleSetJson = """
        {
          "nodes": [
            {
              "data": {
                "condition": "input.order.items.count > 0"
              }
            }
          ]
        }
        """;

        // Act
        RuleSetSchema schema = RuleSetSchemaExtractor.Extract(ruleSetJson, RuleSetKind.FlowGraph);

        // Assert: should capture "order.items.count"
        schema.InputFields.Should().Contain(f => f.Name == "order.items.count",
            "nested path input.order.items.count should be extracted as 'order.items.count'");
    }

    [Fact]
    public void Extract_FlowGraph_SingleLevel_CapturesSimplePath()
    {
        // Arrange: FEEL expression with simple input.amount
        string ruleSetJson = """
        {
          "nodes": [
            {
              "data": {
                "condition": "input.amount > 100"
              }
            }
          ]
        }
        """;

        // Act
        RuleSetSchema schema = RuleSetSchemaExtractor.Extract(ruleSetJson, RuleSetKind.FlowGraph);

        // Assert: simple path still works
        schema.InputFields.Should().Contain(f => f.Name == "amount",
            "simple path input.amount should be extracted as 'amount'");
    }
}
