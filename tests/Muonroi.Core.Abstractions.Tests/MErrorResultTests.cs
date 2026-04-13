namespace Muonroi.Core.Abstractions.Tests;

public class MErrorResultTests
{
    [Fact]
    public void ToString_Includes_ErrorValues_When_Present()
    {
        MErrorResult error = new()
        {
            ErrorCode = "404",
            ErrorMessage = "Not found"
        };
        error.ErrorValues.Add("id");

        error.ToString().Should().Be("[404: Not found (id)]");
    }

    [Fact]
    public void ToString_Omits_ErrorValues_When_Empty()
    {
        MErrorResult error = new()
        {
            ErrorCode = "500",
            ErrorMessage = "server",
            ErrorValues = []
        };

        error.ToString().Should().Be("[500: server]");
    }

    [Fact]
    public void ToString_Formats_Multiple_ErrorValues()
    {
        MErrorResult error = new()
        {
            ErrorCode = "400",
            ErrorMessage = "bad"
        };
        error.ErrorValues.AddRange(["a", "b"]);

        error.ToString().Should().Be("[400: bad (a,b)]");
    }
}
