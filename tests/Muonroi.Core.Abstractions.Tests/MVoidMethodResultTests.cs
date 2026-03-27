namespace Muonroi.Core.Abstractions.Tests;

public class MVoidMethodResultTests
{
    private static void InitializeHelpers()
    {
        MHelpers.Initialize(new ResourceSetting
        {
            [ResourceSettingKeys.Lang] = "en-US"
        }, typeof(MVoidMethodResult).Assembly);
    }

    [Fact]
    public void ErrorMessages_Return_All_Added_Errors()
    {
        MVoidMethodResult result = new();
        MErrorResult first = new() { ErrorCode = "1" };
        MErrorResult second = new() { ErrorCode = "2" };

        result.AddErrorMessage(first);
        result.AddErrorMessage(second);

        result.ErrorMessages.Should().Contain([first, second]);
    }

    [Fact]
    public void AddErrors_Appends_All_Items()
    {
        MVoidMethodResult result = new();

        result.AddErrors([
            new MErrorResult { ErrorCode = "E1" },
            new MErrorResult { ErrorCode = "E2" }
        ]);

        result.ErrorMessages.Should().HaveCount(2);
    }

    [Fact]
    public void AddError_With_Language_Uses_Helper_Message()
    {
        InitializeHelpers();
        MVoidMethodResult result = new();

        result.AddError("ERROR", "en-US");

        result.ErrorMessages.Should().ContainSingle();
        result.ErrorMessages.Single().ErrorCode.Should().Be("ERROR");
    }

    [Fact]
    public void GetErrorMessage_Returns_Default_When_Code_Is_Unknown()
    {
        InitializeHelpers();

        MVoidMethodResult.GetErrorMessage("UNKNOWN", "en-US").Should().Be("No pre-defined error message");
    }

    [Fact]
    public void GetActionResult_Uses_StatusCode_When_Provided()
    {
        MVoidMethodResult result = new()
        {
            StatusCode = StatusCodes.Status400BadRequest
        };

        ObjectResult actionResult = result.GetActionResult().Should().BeOfType<ObjectResult>().Subject;

        actionResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public void FromError_Creates_Result_With_Error()
    {
        MVoidMethodResult result = MVoidMethodResult.FromError("CODE", "v1");

        result.IsOk.Should().BeFalse();
        result.ErrorMessages.Should().ContainSingle();
        result.ErrorMessages.Single().ErrorCode.Should().Be("CODE");
    }
}
