namespace Muonroi.BuildingBlock.Test;

public class MResponseTests
{
    [Fact]
    public void Error_Property_Returns_Set_Value()
    {
        MResponse<string> resp = new();
        MErrorResult err = new()
        {
            ErrorCode = "E1"
        };
        resp.Error = err;
        Assert.Same(err, resp.Error);
    }

    [Fact]
    public void Error_Property_Defaults_To_Null()
    {
        MResponse<string> resp = new();
        Assert.Null(resp.Error);
        resp.Error = null;
        Assert.Null(resp.Error);
    }

    [Fact]
    public void AddErrors_Adds_All_Errors()
    {
        MResponse<string> resp = new();
        MErrorResult e1 = new()
        {
            ErrorCode = "1"
        };
        MErrorResult e2 = new()
        {
            ErrorCode = "2"
        };
        resp.AddErrors([e1, e2]);
        Assert.Contains(e1, resp.ErrorMessages);
        Assert.Contains(e2, resp.ErrorMessages);
    }

    [Fact]
    public void AddErrors_WithEmptyList_DoesNothing()
    {
        MResponse<string> resp = new();
        resp.AddErrors([]);
        Assert.Empty(resp.ErrorMessages);
    }

    [Fact]
    public void AddErrors_Allows_Duplicates()
    {
        MResponse<string> resp = new();
        MErrorResult e1 = new()
        {
            ErrorCode = "1"
        };
        resp.AddErrors([e1, e1]);
        Assert.Equal(2, resp.ErrorMessages.Count);
    }

    [Fact]
    public void SetError_Overload1_Works_And_Overwrites()
    {
        MResponse<string> resp = new();
        resp.SetError("first");
        MErrorResult first = resp.Error!;
        resp.SetError("second");
        Assert.Equal("second", resp.Error!.ErrorCode);
        Assert.NotSame(first, resp.Error);
        Assert.Equal(2, resp.ErrorMessages.Count);
    }

    [Fact]
    public void SetError_Overload1_Allows_Null_Or_Empty_Code()
    {
        MResponse<string> resp = new();
        resp.SetError(null!);
        Assert.Null(resp.Error!.ErrorCode);
        resp.SetError(string.Empty);
        Assert.Equal(string.Empty, resp.Error!.ErrorCode);
    }

    [Fact]
    public void SetError_Overload2_Works_With_Arguments()
    {
        MResponse<string> resp = new();
        resp.SetError("err", ["a", 1]);
        Assert.Equal("err", resp.Error!.ErrorCode);
        Assert.Equal(["a", 1], resp.Error!.ErrorValues);
    }


    [Fact]
    public void SetError_Overload2_Allows_Null_Or_Empty_Code()
    {
        MResponse<string> resp = new();
        resp.SetError(null!, 1);
        Assert.Null(resp.Error!.ErrorCode);
    }

    [Fact]
    public void SetError_Overload3_Works_With_Lang_And_Arguments()
    {
        MResponse<string> resp = new();
        resp.SetError("err", "en", "a");
        Assert.Equal("err", resp.Error!.ErrorCode);
        Assert.Equal(["a"], resp.Error!.ErrorValues);
        Assert.False(string.IsNullOrEmpty(resp.Error!.ErrorMessage));
    }

    [Fact]
    public void SetErrorMessage_Sets_Message_And_Overwrites()
    {
        MResponse<string> resp = new();
        resp.SetErrorMessage("c1", "m1");
        Assert.Equal("m1", resp.Error!.ErrorMessage);
        resp.SetErrorMessage("c1", "m2");
        Assert.Equal("m2", resp.Error!.ErrorMessage);
        Assert.Equal(2, resp.ErrorMessages.Count);
    }

    [Fact]
    public void SetErrorMessage_Uses_Default_Message_When_Null_Or_Empty()
    {
        MResponse<string> resp = new();
        resp.SetErrorMessage("c1", null);
        Assert.Equal("No pre-defined error message", resp.Error!.ErrorMessage);
        resp.SetErrorMessage("c1", string.Empty);
        Assert.Equal("No pre-defined error message", resp.Error!.ErrorMessage);
    }

    [Fact]
    public void GetActionResult_Returns_ObjectResult_With_StatusCode()
    {
        MResponse<string> resp = new()
        {
            StatusCode = 400
        };
        IActionResult result = resp.GetActionResult();
        ObjectResult obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(400, obj.StatusCode);
        Assert.Same(resp, obj.Value);
    }

    [Fact]
    public void GetActionResult_Defaults_To_200_When_No_StatusCode()
    {
        MResponse<string> resp = new();
        IActionResult result = resp.GetActionResult();
        ObjectResult obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, obj.StatusCode);
        Assert.Same(resp, obj.Value);
    }
}
