using Muonroi.RuleGen.Models;
using Muonroi.RuleGen.Services;
using FluentAssertions;
using Xunit;

namespace Muonroi.RuleGen.Tests;

public class RuleValidationServiceTests
{
    private static ExtractedRuleDefinition CreateRule(
        string code,
        string hookPoint = "BeforeRule",
        IReadOnlyList<string>? dependsOn = null,
        string sourceFile = "test.cs",
        int sourceLine = 1)
    {
        return new ExtractedRuleDefinition(
            Code: code,
            MethodName: $"Do{code}",
            ClassName: "TestClass",
            SourceNamespace: "Test",
            OutputNamespace: "Test.Rules",
            ContextType: "TestContext",
            ReturnType: "Task<RuleResult>",
            Order: 0,
            HookPoint: hookPoint,
            DependsOn: dependsOn ?? [],
            Usings: [],
            CustomAttributes: [],
            Parameters: [],
            Dependencies: [],
            HelperMethods: [],
            DocumentationComment: null,
            MethodBody: "return Task.FromResult(RuleResult.Passed());",
            ExpressionBody: null,
            IsAsync: true,
            SourceFile: sourceFile,
            SourceLine: sourceLine);
    }

    [Fact]
    public void Validate_ValidRules_NoErrors()
    {
        List<ExtractedRuleDefinition> rules =
        [
            CreateRule("RULE_001"),
            CreateRule("RULE_002")
        ];

        ValidationReport report = RuleValidationService.Validate(rules);
        report.HasErrors.Should().BeFalse();
        report.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void Validate_DuplicateRuleCodes_ReportsError()
    {
        List<ExtractedRuleDefinition> rules =
        [
            CreateRule("RULE_001", sourceFile: "a.cs", sourceLine: 10),
            CreateRule("RULE_001", sourceFile: "b.cs", sourceLine: 20)
        ];

        ValidationReport report = RuleValidationService.Validate(rules);
        report.HasErrors.Should().BeTrue();
        report.Errors.Should().ContainSingle(e => e.Contains("Duplicate rule code"));
    }

    [Fact]
    public void Validate_InvalidHookPoint_ReportsError()
    {
        List<ExtractedRuleDefinition> rules = [CreateRule("RULE_001", hookPoint: "InvalidHookPoint")];

        ValidationReport report = RuleValidationService.Validate(rules);
        report.HasErrors.Should().BeTrue();
        report.Errors.Should().ContainSingle(e => e.Contains("invalid HookPoint"));
    }

    [Fact]
    public void Validate_ValidHookPoints_NoErrors()
    {
        List<ExtractedRuleDefinition> rules =
        [
            CreateRule("R1", hookPoint: "BeforeRule"),
            CreateRule("R2", hookPoint: "AfterRule"),
            CreateRule("R3", hookPoint: "BeforePersist"),
            CreateRule("R4", hookPoint: "AfterPersist")
        ];

        ValidationReport report = RuleValidationService.Validate(rules);
        report.Errors.Where(e => e.Contains("invalid HookPoint")).Should().BeEmpty();
    }

    [Fact]
    public void Validate_MissingDependency_ReportsWarning()
    {
        List<ExtractedRuleDefinition> rules =
        [
            CreateRule("RULE_001", dependsOn: ["RULE_999"])
        ];

        ValidationReport report = RuleValidationService.Validate(rules);
        report.Warnings.Should().ContainSingle(w => w.Contains("depends on missing rule"));
    }

    [Fact]
    public void Validate_CircularDependency_ReportsError()
    {
        List<ExtractedRuleDefinition> rules =
        [
            CreateRule("A", dependsOn: ["B"]),
            CreateRule("B", dependsOn: ["A"])
        ];

        ValidationReport report = RuleValidationService.Validate(rules);
        report.HasErrors.Should().BeTrue();
        report.Errors.Should().Contain(e => e.Contains("Circular dependency"));
    }

    [Fact]
    public void Validate_TransitiveCircularDependency_ReportsError()
    {
        List<ExtractedRuleDefinition> rules =
        [
            CreateRule("A", dependsOn: ["B"]),
            CreateRule("B", dependsOn: ["C"]),
            CreateRule("C", dependsOn: ["A"])
        ];

        ValidationReport report = RuleValidationService.Validate(rules);
        report.HasErrors.Should().BeTrue();
        report.Errors.Should().Contain(e => e.Contains("Circular dependency"));
    }

    [Fact]
    public void Validate_ValidDependencyChain_NoCircularErrors()
    {
        List<ExtractedRuleDefinition> rules =
        [
            CreateRule("A"),
            CreateRule("B", dependsOn: ["A"]),
            CreateRule("C", dependsOn: ["B"])
        ];

        ValidationReport report = RuleValidationService.Validate(rules);
        report.Errors.Where(e => e.Contains("Circular dependency")).Should().BeEmpty();
    }

    [Fact]
    public void Validate_EmptyRules_NoErrors()
    {
        ValidationReport report = RuleValidationService.Validate([]);
        report.HasErrors.Should().BeFalse();
        report.Warnings.Should().BeEmpty();
    }
}
