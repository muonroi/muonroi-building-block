namespace Muonroi.RuleEngine.Runtime.Tests;

public sealed class RuleSetDefinitionValidatorExtendedTests
{
    private readonly RuleSetDefinitionValidator _sut = new();

    [Fact]
    public void Validate_EmptyWorkflowName_ReturnsError()
    {
        RuleSetValidationResult result = _sut.Validate("", "{}");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == "WorkflowRequired");
    }

    [Fact]
    public void Validate_WhitespaceWorkflowName_ReturnsError()
    {
        RuleSetValidationResult result = _sut.Validate("   ", "{}");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == "WorkflowRequired");
    }

    [Fact]
    public void Validate_EmptyPayload_ReturnsError()
    {
        RuleSetValidationResult result = _sut.Validate("wf1", "");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == "PayloadEmpty");
    }

    [Fact]
    public void Validate_InvalidJson_ReturnsError()
    {
        RuleSetValidationResult result = _sut.Validate("wf1", "{not-json}");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == "InvalidJson");
    }

    [Fact]
    public void Validate_InvalidRootKind_NumberPayload_ReturnsError()
    {
        RuleSetValidationResult result = _sut.Validate("wf1", "42");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == "InvalidRootKind");
    }

    [Fact]
    public void Validate_EmptyArray_ReturnsError()
    {
        RuleSetValidationResult result = _sut.Validate("wf1", "[]");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == "EmptyArray");
    }

    [Fact]
    public void Validate_ArrayOfStrings_ReturnsCodeArrayShape()
    {
        RuleSetValidationResult result = _sut.Validate("wf1", "[\"RuleClass1\", \"RuleClass2\"]");

        result.IsValid.Should().BeTrue();
        result.Shape.Should().Be("CodeArray");
    }

    [Fact]
    public void Validate_ArrayOfNumbers_ReturnsError()
    {
        RuleSetValidationResult result = _sut.Validate("wf1", "[123, 456]");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == "InvalidArrayItem");
    }

    [Fact]
    public void Validate_LegacyWorkflowArray_MatchingName_IsValid()
    {
        string json = """
        [{"WorkflowName":"wf1","Rules":[{"RuleName":"r1"}]}]
        """;

        RuleSetValidationResult result = _sut.Validate("wf1", json);

        result.IsValid.Should().BeTrue();
        result.Shape.Should().Be("LegacyWorkflowArray");
    }

    [Fact]
    public void Validate_LegacyWorkflowArray_MismatchedName_HasError()
    {
        string json = """
        [{"WorkflowName":"wf-other","Rules":[{"RuleName":"r1"}]}]
        """;

        RuleSetValidationResult result = _sut.Validate("wf1", json);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == "WorkflowMismatch");
    }

    [Fact]
    public void Validate_LegacyWorkflowArray_EmptyRules_HasError()
    {
        string json = """
        [{"WorkflowName":"wf1","Rules":[]}]
        """;

        RuleSetValidationResult result = _sut.Validate("wf1", json);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == "RulesMissing");
    }

    [Fact]
    public void Validate_ObjectWithRulesArray_StringItems_CodeWorkflowObject()
    {
        string json = """
        {"workflowName":"wf1","rules":["RuleClass1"]}
        """;

        RuleSetValidationResult result = _sut.Validate("wf1", json);

        result.IsValid.Should().BeTrue();
        result.Shape.Should().Be("CodeWorkflowObject");
    }

    [Fact]
    public void Validate_ObjectWithRulesArray_ObjectItems_LegacyWorkflowObject()
    {
        string json = """
        {"workflowName":"wf1","rules":[{"RuleName":"r1"}]}
        """;

        RuleSetValidationResult result = _sut.Validate("wf1", json);

        result.IsValid.Should().BeTrue();
        result.Shape.Should().Be("LegacyWorkflowObject");
    }

    [Fact]
    public void Validate_ObjectWithEmptyRulesArray_HasError()
    {
        string json = """
        {"workflowName":"wf1","rules":[]}
        """;

        RuleSetValidationResult result = _sut.Validate("wf1", json);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == "RulesEmpty");
    }

    [Fact]
    public void Validate_ObjectWithRuleName_LegacyRuleObject()
    {
        string json = """
        {"RuleName":"r1","Expression":"true"}
        """;

        RuleSetValidationResult result = _sut.Validate("wf1", json);

        result.IsValid.Should().BeTrue();
        result.Shape.Should().Be("LegacyRuleObject");
    }

    [Fact]
    public void Validate_FlowGraphOnly_ValidGraph()
    {
        string json = """
        {"flowGraph":{"nodes":[{"id":"n1","type":"condition"}]}}
        """;

        RuleSetValidationResult result = _sut.Validate("wf1", json);

        result.IsValid.Should().BeTrue();
        result.Shape.Should().Be("FlowGraphOnly");
    }

    [Fact]
    public void Validate_FlowGraphOnly_EmptyNodes_ReturnsError()
    {
        string json = """
        {"flowGraph":{"nodes":[]}}
        """;

        RuleSetValidationResult result = _sut.Validate("wf1", json);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == "RulesMissing");
    }

    [Fact]
    public void Validate_ObjectWithMismatchedWorkflowName_HasError()
    {
        string json = """
        {"workflowName":"wf-wrong","rules":[{"RuleName":"r1"}]}
        """;

        RuleSetValidationResult result = _sut.Validate("wf1", json);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == "WorkflowMismatch");
    }

    [Fact]
    public void Validate_ObjectWithNoRulesOrGraph_HasError()
    {
        string json = """
        {"someProp":"value"}
        """;

        RuleSetValidationResult result = _sut.Validate("wf1", json);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == "RulesMissing");
    }

    [Fact]
    public void Validate_ObjectRulesWithNumberItems_HasError()
    {
        string json = """
        {"rules":[42]}
        """;

        RuleSetValidationResult result = _sut.Validate("wf1", json);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == "RulesItemType");
    }
}
