namespace Muonroi.BuildingBlock.Test;

public class MMethodResultHelpersTests
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
    public void AddApiErrorMessage_WithValues_Adds_Error_And_Allows_Duplicates()
    {
        Init();
        MVoidMethodResult result = new();
        result.AddApiErrorMessage("ERR", ["a"]);
        result.AddApiErrorMessage("ERR", ["a"]);
        Assert.Equal(2, result.ErrorMessages.Count);
        Assert.All(result.ErrorMessages, e => Assert.Equal("ERR", e.ErrorCode));
    }

    [Fact]
    public void AddApiErrorMessage_WithLang_Adds_Error()
    {
        Init();
        MVoidMethodResult result = new();
        result.AddApiErrorMessage("ERR", ["a"], "en-US");
        MErrorResult err = Assert.Single(result.ErrorMessages);
        Assert.Equal("ERR", err.ErrorCode);
    }

    [Fact]
    public void AddApiErrorMessage_WithMessage_Null_Or_Empty_Uses_Default()
    {
        Init();
        MVoidMethodResult result = new();
        result.AddApiErrorMessage("ERR", (string)null!);
        result.AddApiErrorMessage("ERR", string.Empty);
        Assert.Equal(2, result.ErrorMessages.Count);
        MErrorResult first = result.ErrorMessages.First();
        MErrorResult second = result.ErrorMessages.Last();
        Assert.Null(first.ErrorCode);
        Assert.Equal("ERR", first.ErrorMessage);
        Assert.Equal(string.Empty, second.ErrorCode);
        Assert.Equal("ERR", second.ErrorMessage);
    }
}
