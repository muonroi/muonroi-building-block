using Muonroi.RuleEngine.Core;
using Xunit;

namespace Muonroi.RuleEngine.Core.Tests;

public class RuleResultTheoryTests
{
    [Fact]
    public void RuleResult_Passed_IsSuccessful()
    {
        RuleResult result = RuleResult.Passed();

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Errors);
    }

    [Theory]
    [InlineData("Error 1")]
    [InlineData("Validation failed")]
    [InlineData("Business rule violation")]
    [InlineData("Required field missing")]
    [InlineData("Invalid format")]
    public void RuleResult_Failure_WithSingleError_StoresError(string errorMessage)
    {
        RuleResult result = RuleResult.Failure(errorMessage);

        Assert.False(result.IsSuccess);
        Assert.Contains(errorMessage, result.Errors);
        Assert.Single(result.Errors);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public void RuleResult_Failure_WithEmptyError_StillStoresIt(string emptyError)
    {
        RuleResult result = RuleResult.Failure(emptyError);

        Assert.False(result.IsSuccess);
        Assert.Contains(emptyError, result.Errors);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(10)]
    public void RuleResult_Failure_WithMultipleErrors_StoresAllErrors(int errorCount)
    {
        string[] errors = new string[errorCount];
        for (int i = 0; i < errorCount; i++)
        {
            errors[i] = $"Error {i}";
        }

        RuleResult result = RuleResult.Failure(errors);

        Assert.False(result.IsSuccess);
        Assert.Equal(errorCount, result.Errors.Count);
        foreach (string error in errors)
        {
            Assert.Contains(error, result.Errors);
        }
    }

    [Theory]
    [InlineData("unicode_中文_error")]
    [InlineData("unicode_日本語_error")]
    [InlineData("emoji_😀_error")]
    [InlineData("special@chars#error")]
    public void RuleResult_Failure_WithUnicodeErrors_StoresCorrectly(string unicodeError)
    {
        RuleResult result = RuleResult.Failure(unicodeError);

        Assert.False(result.IsSuccess);
        Assert.Contains(unicodeError, result.Errors);
    }

    [Theory]
    [InlineData("Error with newline\n")]
    [InlineData("Error with tab\t")]
    [InlineData("Error with multiple\n\nlines")]
    public void RuleResult_Failure_WithSpecialCharacters_StoresCorrectly(string specialError)
    {
        RuleResult result = RuleResult.Failure(specialError);

        Assert.False(result.IsSuccess);
        Assert.Contains(specialError, result.Errors);
    }

    [Fact]
    public void RuleResult_Passed_HasEmptyErrorList()
    {
        RuleResult result = RuleResult.Passed();

        Assert.NotNull(result.Errors);
        Assert.Empty(result.Errors);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(500)]
    [InlineData(1000)]
    public void RuleResult_Failure_WithLongErrorMessage_StoresCorrectly(int messageLength)
    {
        string longError = new('x', messageLength);
        RuleResult result = RuleResult.Failure(longError);

        Assert.False(result.IsSuccess);
        Assert.Contains(longError, result.Errors);
        Assert.Equal(messageLength, result.Errors[0].Length);
    }

    [Theory]
    [InlineData("Error1", "Error2")]
    [InlineData("Validation1", "Validation2")]
    [InlineData("A", "B")]
    public void RuleResult_Failure_WithTwoErrors_StoresBoth(string error1, string error2)
    {
        RuleResult result = RuleResult.Failure([error1, error2]);

        Assert.False(result.IsSuccess);
        Assert.Contains(error1, result.Errors);
        Assert.Contains(error2, result.Errors);
        Assert.Equal(2, result.Errors.Count);
    }

    [Fact]
    public void RuleResult_Passed_CanBeCreatedMultipleTimes()
    {
        RuleResult result1 = RuleResult.Passed();
        RuleResult result2 = RuleResult.Passed();

        Assert.True(result1.IsSuccess);
        Assert.True(result2.IsSuccess);
    }

    [Theory]
    [InlineData("Same Error")]
    [InlineData("Duplicate")]
    public void RuleResult_Failure_WithDuplicateErrors_StoresBoth(string duplicateError)
    {
        RuleResult result = RuleResult.Failure([duplicateError, duplicateError]);

        Assert.False(result.IsSuccess);
        Assert.Equal(2, result.Errors.Count);
    }
}
