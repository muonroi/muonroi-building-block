using FluentAssertions;
using Muonroi.RuleEngine.Runtime.Rules;
using Xunit;

namespace Muonroi.RuleEngine.Runtime.Tests;

public sealed class RuleSetDefinitionValidatorTests
{
    private readonly RuleSetDefinitionValidator _validator = new();

    [Fact]
    public void Validate_ShouldFail_WhenWorkflowMismatched()
    {
        const string json = """
                            {
                              "workflowName": "other-workflow",
                              "rules": [
                                "RULE_A"
                              ]
                            }
                            """;

        RuleSetValidationResult result = _validator.Validate("expected-workflow", json);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.Code == "WorkflowMismatch");
    }

    [Fact]
    public void Validate_ShouldPass_ForCodeWorkflowObject()
    {
        const string json = """
                            {
                              "workflowName": "payment",
                              "rules": [
                                "CHECK_STATUS",
                                "CHECK_LIMIT"
                              ]
                            }
                            """;

        RuleSetValidationResult result = _validator.Validate("payment", json);

        result.IsValid.Should().BeTrue();
        result.Shape.Should().Be("CodeWorkflowObject");
    }

    [Fact]
    public void Validate_ShouldPass_ForLegacyWorkflowArray()
    {
        const string json = """
                            [
                              {
                                "WorkflowName": "pricing",
                                "Rules": [
                                  {
                                    "RuleName": "R1",
                                    "RuleExpressionType": "LambdaExpression",
                                    "Expression": "input1.value > 0"
                                  }
                                ]
                              }
                            ]
                            """;

        RuleSetValidationResult result = _validator.Validate("pricing", json);

        result.IsValid.Should().BeTrue();
        result.Shape.Should().Be("LegacyWorkflowArray");
    }
}
