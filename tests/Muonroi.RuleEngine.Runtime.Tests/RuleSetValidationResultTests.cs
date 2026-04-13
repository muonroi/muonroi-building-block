using Muonroi.Core.Abstractions.Exceptions;
using FluentAssertions;
using Muonroi.RuleEngine.Runtime.Rules;
using Xunit;

namespace Muonroi.RuleEngine.Runtime.Tests;

public sealed class RuleSetValidationResultTests
{
    [Fact]
    public void IsValid_NoErrors_ReturnsTrue()
    {
        RuleSetValidationResult result = new() { WorkflowName = "wf1" };

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void IsValid_WithErrors_ReturnsFalse()
    {
        RuleSetValidationResult result = new() { WorkflowName = "wf1" };
        result.Errors.Add(new RuleSetValidationIssue("code", "message"));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void ThrowIfInvalid_NoErrors_DoesNotThrow()
    {
        RuleSetValidationResult result = new() { WorkflowName = "wf1" };

        Action act = () => result.ThrowIfInvalid();

        act.Should().NotThrow();
    }

    [Fact]
    public void ThrowIfInvalid_WithErrors_ThrowsInvalidDataException()
    {
        RuleSetValidationResult result = new() { WorkflowName = "wf1" };
        result.Errors.Add(new RuleSetValidationIssue("E1", "Error one"));
        result.Errors.Add(new RuleSetValidationIssue("E2", "Error two"));

        Action act = () => result.ThrowIfInvalid();

        act.Should().Throw<MConfigurationException>()
            .WithMessage("*Error one*")
            .WithMessage("*Error two*");
    }

    [Fact]
    public void DefaultShape_IsUnknown()
    {
        RuleSetValidationResult result = new();

        result.Shape.Should().Be("Unknown");
    }

    [Fact]
    public void DefaultWorkflowName_IsEmpty()
    {
        RuleSetValidationResult result = new();

        result.WorkflowName.Should().BeEmpty();
    }

    [Fact]
    public void Warnings_CanBeAdded()
    {
        RuleSetValidationResult result = new();
        result.Warnings.Add(new RuleSetValidationIssue("W1", "A warning", Severity: "Warning"));

        result.Warnings.Should().HaveCount(1);
        result.IsValid.Should().BeTrue(); // warnings don't affect validity
    }
}
