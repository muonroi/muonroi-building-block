namespace Muonroi.Core.Abstractions.Tests;

public class MResponseTests
{
    [Fact]
    public void NewResponse_IsOk_True_And_NoErrors()
    {
        MResponse<string> response = new();

        response.IsOk.Should().BeTrue();
        response.ErrorMessages.Should().BeEmpty();
        response.Result.Should().BeNull();
        response.Error.Should().BeNull();
    }

    [Fact]
    public void SetError_SetsErrorCode_And_AddsToErrorMessages()
    {
        MResponse<int> response = new();
        response.SetError("ERR001");

        response.IsOk.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.ErrorCode.Should().Be("ERR001");
        response.ErrorMessages.Should().HaveCount(1);
    }

    [Fact]
    public void SetError_WithArguments_IncludesArguments()
    {
        MResponse<string> response = new();
        response.SetError("ERR002", new object[] { "arg1", "arg2" });

        response.Error.Should().NotBeNull();
        response.Error!.ErrorCode.Should().Be("ERR002");
        response.Error.ErrorValues.Should().Contain("arg1");
        response.Error.ErrorValues.Should().Contain("arg2");
    }

    [Fact]
    public void SetError_WithLanguage_Overload_Treats_Second_String_As_Language()
    {
        MResponse<string> response = new();
        response.SetError("ERR002", "en", "arg2");

        response.Error.Should().NotBeNull();
        response.Error!.ErrorCode.Should().Be("ERR002");
        response.Error.ErrorValues.Should().ContainSingle().Which.Should().Be("arg2");
    }

    [Fact]
    public void SetErrorMessage_WithCustomMessage_SetsErrorMessage()
    {
        MResponse<string> response = new();
        response.SetErrorMessage("ERR003", "Custom error message");

        response.Error.Should().NotBeNull();
        response.Error!.ErrorCode.Should().Be("ERR003");
        response.Error.ErrorMessage.Should().Be("Custom error message");
    }

    [Fact]
    public void SetErrorMessage_WithNullMessage_FallsBackToErrorCode()
    {
        MResponse<string> response = new();
        response.SetErrorMessage("ERR004", null);

        response.Error.Should().NotBeNull();
        response.Error!.ErrorCode.Should().Be("ERR004");
        response.Error.ErrorMessage.Should().NotBeNull();
    }

    [Fact]
    public void AddErrors_AddsMultipleErrors()
    {
        MResponse<string> response = new();
        List<MErrorResult> errors =
        [
            new() { ErrorCode = "E1", ErrorMessage = "Error 1" },
            new() { ErrorCode = "E2", ErrorMessage = "Error 2" }
        ];

        response.AddErrors(errors);

        response.IsOk.Should().BeFalse();
        response.ErrorMessages.Should().HaveCount(2);
    }

    [Fact]
    public void Result_CanBeSetAndRetrieved()
    {
        MResponse<int> response = new() { Result = 42 };

        response.Result.Should().Be(42);
        response.IsOk.Should().BeTrue();
    }

    [Fact]
    public void GetActionResult_ReturnsObjectResult_WithStatusCode200_WhenNoStatusCodeSet()
    {
        MResponse<string> response = new() { Result = "test" };

        IActionResult actionResult = response.GetActionResult();

        ObjectResult objectResult = actionResult.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(200);
    }

    [Fact]
    public void GetActionResult_UsesCustomStatusCode_WhenSet()
    {
        MResponse<string> response = new() { StatusCode = 404 };

        IActionResult actionResult = response.GetActionResult();

        ObjectResult objectResult = actionResult.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(404);
    }
}
