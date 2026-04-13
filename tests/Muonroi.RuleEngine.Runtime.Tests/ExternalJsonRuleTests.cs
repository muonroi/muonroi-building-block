using FluentAssertions;
using Muonroi.RuleEngine.Runtime.Rules;
using RulesEngine.Models;
using Xunit;

namespace Muonroi.RuleEngine.Runtime.Tests;

public sealed class ExternalJsonRuleTests
{
    [Fact]
    public async Task IsSatisfiedAsync_WhenAllRulesPass_ReturnsTrue()
    {
        const string json = """
                            [
                              {
                                "WorkflowName": "eligibility",
                                "Rules": [
                                  {
                                    "RuleName": "age-check",
                                    "Expression": "input1.value.Age >= 18"
                                  }
                                ]
                              }
                            ]
                            """;

        ExternalJsonRule<TestApplicant> sut = new(json, "eligibility");

        bool result = await sut.IsSatisfiedAsync(new TestApplicant(21));

        result.Should().BeTrue();
        sut.Code.Should().Be("eligibility");
    }

    [Fact]
    public async Task IsSatisfiedAsync_WhenAnyRuleFails_ReturnsFalse()
    {
        const string json = """
                            [
                              {
                                "WorkflowName": "eligibility",
                                "Rules": [
                                  {
                                    "RuleName": "age-check",
                                    "Expression": "input1.value.Age >= 18"
                                  },
                                  {
                                    "RuleName": "vip-check",
                                    "Expression": "input1.value.IsVip == true"
                                  }
                                ]
                              }
                            ]
                            """;

        ExternalJsonRule<TestApplicant> sut = new(json, "eligibility");

        bool result = await sut.IsSatisfiedAsync(new TestApplicant(30, IsVip: false));

        result.Should().BeFalse();
    }

    [Fact]
    public void Constructor_WhenSettingsNotProvided_StillInitializes()
    {
        const string json = """
                            [
                              {
                                "WorkflowName": "wf",
                                "Rules": [
                                  {
                                    "RuleName": "always-pass",
                                    "Expression": "true"
                                  }
                                ]
                              }
                            ]
                            """;

        Action action = () => _ = new ExternalJsonRule<TestApplicant>(json, "wf");

        action.Should().NotThrow();
    }

    [Fact]
    public void Constructor_WhenJsonIsInvalid_ThrowsJsonException()
    {
        Action action = () => _ = new ExternalJsonRule<TestApplicant>("not-json", "wf", new ReSettings());

        action.Should().Throw<System.Text.Json.JsonException>();
    }

    private sealed record TestApplicant(int Age, bool IsVip = true);
}
