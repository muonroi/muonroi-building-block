using Muonroi.Rules;

namespace Muonroi.Rules.Tests;

[Collection("NonParallel")]
public class ExternalJsonRuleTests
{
    [Fact]
    public async Task IsSatisfiedAsync_ReturnsTrue_WhenRulePasses()
    {
        const string json = """
                            [
                              {
                                "WorkflowName": "EvenWorkflow",
                                "Rules": [
                                  {
                                    "RuleName": "IsEven",
                                    "RuleExpressionType": "LambdaExpression",
                                    "Expression": "input1.value % 2 == 0"
                                  }
                                ]
                              }
                            ]
                            """;
        ExternalJsonRule<int> rule = new(json, "EvenWorkflow");

        bool result = await rule.IsSatisfiedAsync(4);

        Assert.True(result);
    }

    [Fact]
    public async Task IsSatisfiedAsync_ReturnsFalse_WhenRuleFails()
    {
        const string json = """
                            [
                              {
                                "WorkflowName": "EvenWorkflow",
                                "Rules": [
                                  {
                                    "RuleName": "IsEven",
                                    "RuleExpressionType": "LambdaExpression",
                                    "Expression": "input1.value % 2 == 0"
                                  }
                                ]
                              }
                            ]
                            """;
        ExternalJsonRule<int> rule = new(json, "EvenWorkflow");

        bool result = await rule.IsSatisfiedAsync(3);

        Assert.False(result);
    }

    [Fact]
    public async Task IsSatisfiedAsync_MissingFact_ReturnsFalse()
    {
        const string json = """
                            [
                              {
                                "WorkflowName": "MissingWorkflow",
                                "Rules": [
                                  {
                                    "RuleName": "NeedsSecondInput",
                                    "RuleExpressionType": "LambdaExpression",
                                    "Expression": "input2.value > 0"
                                  }
                                ]
                              }
                            ]
                            """;
        ExternalJsonRule<int> rule = new(json, "MissingWorkflow");

        bool result = await rule.IsSatisfiedAsync(1);

        Assert.False(result);
    }
}
