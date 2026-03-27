using FluentAssertions;
using Muonroi.RuleGen.Models;
using Muonroi.RuleGen.Writers;
using Xunit;

namespace Muonroi.RuleGen.Tests;

/// <summary>
/// Tests for RuleClassWriter generating MFactBagAwareRule-based rules.
/// </summary>
public sealed class FactBagAwareWriterTests
{
    private static ExtractedRuleDefinition CreateDefinition(
        bool useFactBagAware = false,
        string? methodBody = null,
        string returnType = "Task<RuleResult>")
    {
        return new ExtractedRuleDefinition(
            Code: "TEST_RULE",
            MethodName: "EvaluateTestAsync",
            ClassName: "TestHandler",
            SourceNamespace: "Test.Namespace",
            OutputNamespace: "Test.Generated",
            ContextType: "TestContext",
            ReturnType: returnType,
            Order: 1,
            HookPoint: "BeforeRule",
            DependsOn: [],
            Usings: ["System"],
            CustomAttributes: [],
            Parameters:
            [
                new ParameterModel("ctx", "TestContext", false, null),
                new ParameterModel("facts", "FactBag", false, null),
                new ParameterModel("ct", "CancellationToken", false, null)
            ],
            Dependencies: [],
            HelperMethods: [],
            DocumentationComment: null,
            MethodBody: methodBody ?? @"{
    var gateResult = facts.Get<bool>(""gate.result"");
    facts.Set(""output.valid"", gateResult);
    return Task.FromResult(RuleResult.Passed());
}",
            ExpressionBody: null,
            IsAsync: false,
            SourceFile: "TestHandler.cs",
            SourceLine: 10,
            UseFactBagAware: useFactBagAware);
    }

    [Fact]
    public void IRule_mode_generates_standard_interface_implementation()
    {
        // Arrange
        ExtractedRuleDefinition definition = CreateDefinition(useFactBagAware: false);

        // Act
        string output = RuleClassWriter.Render(definition, null, null, null);

        // Assert
        output.Should().Contain(": IRule<TestContext>");
        output.Should().Contain("public async Task<RuleResult> EvaluateAsync(TestContext ctx, FactBag facts, CancellationToken ct)");
        output.Should().Contain("public Task ExecuteAsync(");
        output.Should().Contain("facts.Get<bool>");
        output.Should().Contain("facts.Set(");
    }

    [Fact]
    public void FactBagAware_mode_generates_MFactBagAwareRule_inheritance()
    {
        // Arrange
        ExtractedRuleDefinition definition = CreateDefinition(useFactBagAware: true);

        // Act
        string output = RuleClassWriter.Render(definition, null, null, null);

        // Assert
        output.Should().Contain(": MFactBagAwareRule<TestContext>");
        output.Should().NotContain(": IRule<TestContext>");
    }

    [Fact]
    public void FactBagAware_mode_uses_EvaluateCoreAsync_override()
    {
        // Arrange
        ExtractedRuleDefinition definition = CreateDefinition(useFactBagAware: true);

        // Act
        string output = RuleClassWriter.Render(definition, null, null, null);

        // Assert
        output.Should().Contain("protected async override Task<RuleResult> EvaluateCoreAsync(TestContext ctx, CancellationToken ct)");
        output.Should().NotContain("public Task<RuleResult> EvaluateAsync(");
    }

    [Fact]
    public void FactBagAware_mode_omits_ExecuteAsync()
    {
        // Arrange
        ExtractedRuleDefinition definition = CreateDefinition(useFactBagAware: true);

        // Act
        string output = RuleClassWriter.Render(definition, null, null, null);

        // Assert
        output.Should().NotContain("public Task ExecuteAsync(");
    }

    [Fact]
    public void FactBagAware_mode_transforms_facts_Get_to_ReadFact()
    {
        // Arrange
        ExtractedRuleDefinition definition = CreateDefinition(useFactBagAware: true);

        // Act
        string output = RuleClassWriter.Render(definition, null, null, null);

        // Assert
        output.Should().Contain("ReadFact<bool>");
        output.Should().NotContain("facts.Get<");
    }

    [Fact]
    public void FactBagAware_mode_transforms_facts_Set_to_WriteFact()
    {
        // Arrange
        ExtractedRuleDefinition definition = CreateDefinition(useFactBagAware: true);

        // Act
        string output = RuleClassWriter.Render(definition, null, null, null);

        // Assert
        output.Should().Contain("WriteFact(");
        output.Should().NotContain("facts.Set(");
    }

    [Fact]
    public void FactBagAware_mode_uses_override_properties()
    {
        // Arrange
        ExtractedRuleDefinition definition = CreateDefinition(useFactBagAware: true);

        // Act
        string output = RuleClassWriter.Render(definition, null, null, null);

        // Assert
        output.Should().Contain("public override string Code =>");
        output.Should().Contain("public override int Order =>");
        output.Should().Contain("public override IReadOnlyList<string> DependsOn =>");
    }

    [Fact]
    public void FactBagAware_mode_removes_FactBag_from_local_function_params()
    {
        // Arrange
        ExtractedRuleDefinition definition = CreateDefinition(useFactBagAware: true);

        // Act
        string output = RuleClassWriter.Render(definition, null, null, null);

        // Assert — local function should have ctx and ct but NOT facts
        output.Should().Contain("__source_EvaluateTestAsync(TestContext ctx, CancellationToken ct)");
        output.Should().NotContain("__source_EvaluateTestAsync(TestContext ctx, FactBag facts, CancellationToken ct)");
    }

    [Fact]
    public void FactBagAware_mode_transforms_store_result_for_generic_return_type()
    {
        // Arrange
        ExtractedRuleDefinition definition = CreateDefinition(
            useFactBagAware: true,
            returnType: "Task<int>",
            methodBody: @"{
    return Task.FromResult(42);
}");

        // Act
        string output = RuleClassWriter.Render(definition, null, null, null);

        // Assert
        output.Should().Contain("WriteFact(\"TEST_RULE:result\", __value);");
        output.Should().NotContain("facts[\"TEST_RULE:result\"]");
    }
}
