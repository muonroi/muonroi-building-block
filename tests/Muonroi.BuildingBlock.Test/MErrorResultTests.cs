namespace Muonroi.BuildingBlock.Test;

public class MErrorResultTests
{
    [Fact]
    public void ToString_Returns_ErrorDescription_WithValues()
    {
        MErrorResult err = new()
        {
            ErrorCode = "404",
            ErrorMessage = "Not found"
        };
        err.ErrorValues.Add("id");
        string expected = "[404: Not found (id)]";

        Assert.Equal(expected, err.ToString());
    }

    [Fact]
    public void ToString_Returns_ErrorDescription_WhenValuesNull()
    {
        MErrorResult err = new()
        {
            ErrorCode = "500",
            ErrorMessage = "server",
            ErrorValues = null!
        };
        string expected = "[500: server]";

        Assert.Equal(expected, err.ToString());
    }

    [Fact]
    public void ToString_Returns_ErrorDescription_WithMultipleValues()
    {
        MErrorResult err = new()
        {
            ErrorCode = "400",
            ErrorMessage = "bad"
        };
        err.ErrorValues.AddRange(["a", "b"]);
        string expected = "[400: bad (a,b)]";

        Assert.Equal(expected, err.ToString());
    }
}
