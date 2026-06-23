namespace Muonroi.Rules.Tests.Rules;

public class ExternalJsonRuleTests
{
    // NOTE: ExternalJsonRule uses System.Text.Json to deserialize Workflow[],
    // but MS RulesEngine's RuleExpressionType enum requires integer values
    // (not string names) when using System.Text.Json without JsonStringEnumConverter.
    // The integer value 0 = LambdaExpression.

    private const string EvenWorkflowJson = """
    [
        {
            "WorkflowName": "EvenWorkflow",
            "Rules": [
                {
                    "RuleName": "IsEven",
                    "RuleExpressionType": 0,
                    "Expression": "input1.value % 2 == 0"
                }
            ]
        }
    ]
    """;

    [Fact]
    public void Constructor_ShouldSetCode()
    {
        var rule = new ExternalJsonRule<int>(EvenWorkflowJson, "EvenWorkflow");
        rule.Code.Should().Be("EvenWorkflow");
    }

    [Fact]
    public async Task IsSatisfiedAsync_RulePasses_ShouldReturnTrue()
    {
        var rule = new ExternalJsonRule<int>(EvenWorkflowJson, "EvenWorkflow");

        bool result = await rule.IsSatisfiedAsync(4);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsSatisfiedAsync_RuleFails_ShouldReturnFalse()
    {
        var rule = new ExternalJsonRule<int>(EvenWorkflowJson, "EvenWorkflow");

        bool result = await rule.IsSatisfiedAsync(3);

        result.Should().BeFalse();
    }

    [Fact]
    public void Constructor_EmptyArray_ShouldNotThrow()
    {
        var rule = new ExternalJsonRule<object>("[]", "Empty");
        rule.Code.Should().Be("Empty");
    }

    [Fact]
    public void Constructor_StringEnumValue_ShouldNotThrow()
    {
        // MS RulesEngine doesn't support string enum values with System.Text.Json by default,
        // but with modern System.Text.Json or custom setup it may not throw.
        const string json = """
        [
            {
                "WorkflowName": "Test",
                "Rules": [
                    {
                        "RuleName": "R1",
                        "RuleExpressionType": "LambdaExpression",
                        "Expression": "true"
                    }
                ]
            }
        ]
        """;

        Action act = () => new ExternalJsonRule<object>(json, "Test");
        act.Should().NotThrow();
    }
}
