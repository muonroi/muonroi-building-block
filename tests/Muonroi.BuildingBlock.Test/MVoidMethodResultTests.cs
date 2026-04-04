using Muonroi.Core.Abstractions.Exceptions;
namespace Muonroi.BuildingBlock.Test;

public class MVoidMethodResultTests
{
    private static void Init()
    {
        ResourceSetting setting = new()
        {
            [ResourceSettingKeys.Lang] = "en-US"
        };
        MHelpers.Initialize(setting, new MJsonSerializeService(), typeof(MVoidMethodResult).Assembly);
    }

    [Fact]
    public void ErrorMessages_Returns_All_Added_Errors()
    {
        MVoidMethodResult result = new();
        MErrorResult e1 = new()
        {
            ErrorCode = "1"
        };
        MErrorResult e2 = new()
        {
            ErrorCode = "2"
        };
        result.AddErrorMessage(e1);
        result.AddErrorMessage(e2);
        Assert.Equal(2, result.ErrorMessages.Count);
        Assert.Contains(e1, result.ErrorMessages);
        Assert.Contains(e2, result.ErrorMessages);
    }

    [Fact]
    public void ErrorMessages_Empty_When_No_Error()
    {
        MVoidMethodResult result = new();
        Assert.Empty(result.ErrorMessages);
    }

    [Fact]
    public void StatusCode_Getter_Setter_Works()
    {
        MVoidMethodResult result = new();
        Assert.Null(result.StatusCode);
        result.StatusCode = 500;
        Assert.Equal(500, result.StatusCode);
    }

    [Fact]
    public void AddErrors_Adds_All_Errors()
    {
        MVoidMethodResult result = new();
        MErrorResult errorResult = new()
        {
            ErrorCode = "E1"
        };
        result.AddErrors([errorResult, new MErrorResult { ErrorCode = "E2" }]);
        Assert.Equal(2, result.ErrorMessages.Count);
    }

    [Fact]
    public void AddErrors_Null_Throws()
    {
        MVoidMethodResult result = new();
        Assert.Throws<NullReferenceException>(() => result.AddErrors(null!));
    }

    [Fact]
    public void AddErrors_Allows_Duplicates()
    {
        MErrorResult err = new()
        {
            ErrorCode = "E1"
        };
        MVoidMethodResult result = new();
        result.AddErrors([err, err]);
        Assert.Equal(2, result.ErrorMessages.Count);
    }

    [Fact]
    public void AddError_WithLang_Adds_Error()
    {
        Init();
        MVoidMethodResult result = new();
        result.AddError("ERROR", "en-US", "v1");
        Assert.Single(result.ErrorMessages);
        Assert.Equal("ERROR", result.ErrorMessages.First().ErrorCode);
    }

    [Fact]
    public void AddError_WithLang_Null_Code_Throws()
    {
        Init();
        MVoidMethodResult result = new();
        Assert.Throws<MArgumentException>(() => result.AddError(null!, "en-US"));
    }

    [Fact]
    public void AddError_WithArgs_Adds_Error()
    {
        MVoidMethodResult result = new();
        result.AddError("CODE", "one", "two");
        Assert.Single(result.ErrorMessages);
    }

    [Fact]
    public void AddError_WithArgs_Null_Code_Throws()
    {
        MVoidMethodResult result = new();
        Assert.Throws<MArgumentException>(() => result.AddError(null!, "1"));
    }

    [Fact]
    public void GetErrorMessage_Returns_Message_When_Code_Exists()
    {
        Init();
        string msg = MVoidMethodResult.GetErrorMessage("ERROR", "en-US");
        Assert.False(string.IsNullOrEmpty(msg));
    }

    [Fact]
    public void GetErrorMessage_Returns_Default_When_Missing()
    {
        Init();
        string msg = MVoidMethodResult.GetErrorMessage("UNKNOWN", "en-US");
        Assert.Equal("No pre-defined error message", msg);
    }

    [Fact]
    public void GetActionResult_Returns_StatusCode()
    {
        MVoidMethodResult result = new();
        IActionResult act = result.GetActionResult();
        ObjectResult obj = Assert.IsType<ObjectResult>(act);
        Assert.Equal(StatusCodes.Status200OK, obj.StatusCode);

        result.StatusCode = StatusCodes.Status400BadRequest;
        act = result.GetActionResult();
        obj = Assert.IsType<ObjectResult>(act);
        Assert.Equal(StatusCodes.Status400BadRequest, obj.StatusCode);
    }
}
