using System;
using System.Linq;
using Xunit;

namespace Muonroi.RuleEngine.Abstractions.Tests;

public class ExecutionModeTests
{
    [Fact]
    public void Default_value_is_all_or_nothing()
    {
        // Arrange
        var mode = default(ExecutionMode);

        // Act
        var result = mode;

        // Assert
        Assert.Equal(ExecutionMode.AllOrNothing, result);
    }

    [Fact]
    public void All_or_nothing_has_value_zero()
    {
        // Arrange
        var mode = ExecutionMode.AllOrNothing;

        // Act
        var value = (int)mode;

        // Assert
        Assert.Equal(0, value);
    }

    [Fact]
    public void Best_effort_has_value_one()
    {
        // Arrange
        var mode = ExecutionMode.BestEffort;

        // Act
        var value = (int)mode;

        // Assert
        Assert.Equal(1, value);
    }

    [Fact]
    public void Compensate_on_failure_has_value_two()
    {
        // Arrange
        var mode = ExecutionMode.CompensateOnFailure;

        // Act
        var value = (int)mode;

        // Assert
        Assert.Equal(2, value);
    }

    [Theory]
    [InlineData(ExecutionMode.AllOrNothing)]
    [InlineData(ExecutionMode.BestEffort)]
    [InlineData(ExecutionMode.CompensateOnFailure)]
    public void Enum_is_defined_for_known_values(ExecutionMode mode)
    {
        // Arrange
        var value = (int)mode;

        // Act
        var defined = Enum.IsDefined(typeof(ExecutionMode), value);

        // Assert
        Assert.True(defined);
    }

    [Fact]
    public void Enum_is_not_defined_for_negative_value()
    {
        // Arrange
        var value = -1;

        // Act
        var defined = Enum.IsDefined(typeof(ExecutionMode), value);

        // Assert
        Assert.False(defined);
    }

    [Fact]
    public void Enum_is_not_defined_for_out_of_range_value()
    {
        // Arrange
        var value = 99;

        // Act
        var defined = Enum.IsDefined(typeof(ExecutionMode), value);

        // Assert
        Assert.False(defined);
    }

    [Theory]
    [InlineData(ExecutionMode.AllOrNothing, "AllOrNothing")]
    [InlineData(ExecutionMode.BestEffort, "BestEffort")]
    [InlineData(ExecutionMode.CompensateOnFailure, "CompensateOnFailure")]
    public void To_string_returns_expected_name(ExecutionMode mode, string expected)
    {
        // Arrange
        var value = mode;

        // Act
        var name = value.ToString();

        // Assert
        Assert.Equal(expected, name);
    }

    [Theory]
    [InlineData("AllOrNothing", ExecutionMode.AllOrNothing)]
    [InlineData("BestEffort", ExecutionMode.BestEffort)]
    [InlineData("CompensateOnFailure", ExecutionMode.CompensateOnFailure)]
    public void Parse_returns_expected_value(string input, ExecutionMode expected)
    {
        // Arrange
        var name = input;

        // Act
        var value = Enum.Parse<ExecutionMode>(name);

        // Assert
        Assert.Equal(expected, value);
    }

    [Fact]
    public void Parse_is_case_sensitive_by_default()
    {
        // Arrange
        var input = "besteffort";

        // Act
        void Act() => Enum.Parse<ExecutionMode>(input);

        // Assert
        Assert.Throws<ArgumentException>(Act);
    }

    [Theory]
    [InlineData("allornothing", ExecutionMode.AllOrNothing)]
    [InlineData("BESTEFFORT", ExecutionMode.BestEffort)]
    public void Parse_with_ignore_case_succeeds(string input, ExecutionMode expected)
    {
        // Arrange
        var name = input;

        // Act
        var value = Enum.Parse<ExecutionMode>(name, ignoreCase: true);

        // Assert
        Assert.Equal(expected, value);
    }

    [Fact]
    public void Try_parse_returns_true_for_valid_value()
    {
        // Arrange
        var input = "AllOrNothing";

        // Act
        var success = Enum.TryParse(input, out ExecutionMode value);

        // Assert
        Assert.True(success);
        Assert.Equal(ExecutionMode.AllOrNothing, value);
    }

    [Fact]
    public void Try_parse_returns_false_for_invalid_value()
    {
        // Arrange
        var input = "NotAValue";

        // Act
        var success = Enum.TryParse(input, out ExecutionMode value);

        // Assert
        Assert.False(success);
        Assert.Equal(default, value);
    }

    [Fact]
    public void Cast_from_int_to_enum_allows_undefined_values()
    {
        // Arrange
        var raw = 42;

        // Act
        var mode = (ExecutionMode)raw;

        // Assert
        Assert.Equal(raw, (int)mode);
        Assert.False(Enum.IsDefined(typeof(ExecutionMode), mode));
    }

    [Fact]
    public void Get_names_contains_expected_members()
    {
        // Arrange
        var names = Enum.GetNames<ExecutionMode>();

        // Act
        var set = names.ToHashSet(StringComparer.Ordinal);

        // Assert
        Assert.True(set.SetEquals(new[] { "AllOrNothing", "BestEffort", "CompensateOnFailure" }));
    }
}
