namespace Muonroi.Pdf.Tests.Layout;

/// <summary>
/// Phase 14 (Group B): CSS <c>linear-gradient(...)</c> parsing — angle/direction forms, color stops
/// with optional percentage positions, and function-valued colors (rgb()) whose internal commas
/// must not split stops.
/// </summary>
public sealed class LinearGradientParserTests
{
    [Fact]
    public void AngleAndTwoStops_AreParsed()
    {
        LinearGradientParser.TryParse("linear-gradient(90deg, #ffffff, #000000)", out LinearGradient g)
            .Should().BeTrue();
        g.AngleDegrees.Should().Be(90f);
        g.Stops.Should().HaveCount(2);
        g.Stops[0].Color.Should().Be("#ffffff");
        g.Stops[1].Color.Should().Be("#000000");
    }

    [Fact]
    public void NoDirection_DefaultsToBottom_180deg()
    {
        LinearGradientParser.TryParse("linear-gradient(#fff, #000)", out LinearGradient g).Should().BeTrue();
        g.AngleDegrees.Should().Be(180f, because: "the CSS default gradient direction is 'to bottom'");
    }

    [Theory]
    [InlineData("to right", 90f)]
    [InlineData("to top", 0f)]
    [InlineData("to left", 270f)]
    [InlineData("to bottom right", 135f)]
    public void ToSideDirections_MapToAngles(string side, float expected)
    {
        LinearGradientParser.TryParse($"linear-gradient({side}, #fff, #000)", out LinearGradient g)
            .Should().BeTrue();
        g.AngleDegrees.Should().Be(expected);
    }

    [Fact]
    public void StopPositions_AreParsedAsFractions()
    {
        LinearGradientParser.TryParse("linear-gradient(#fff 0%, #000 100%)", out LinearGradient g)
            .Should().BeTrue();
        g.Stops[0].Position.Should().Be(0f);
        g.Stops[1].Position.Should().Be(1f);
    }

    [Fact]
    public void RgbStops_KeepInternalCommas()
    {
        LinearGradientParser.TryParse("linear-gradient(45deg, rgb(255, 0, 0), rgb(0, 0, 255))", out LinearGradient g)
            .Should().BeTrue();
        g.Stops.Should().HaveCount(2, because: "commas inside rgb() must not split the stop list");
        g.Stops[0].Color.Should().Be("rgb(255, 0, 0)");
        g.Stops[1].Color.Should().Be("rgb(0, 0, 255)");
    }

    [Fact]
    public void SingleStop_IsRejected()
    {
        LinearGradientParser.TryParse("linear-gradient(#fff)", out _)
            .Should().BeFalse(because: "a gradient needs at least two color stops");
    }

    [Fact]
    public void NonLinearGradient_IsRejected()
    {
        LinearGradientParser.TryParse("radial-gradient(#fff, #000)", out _).Should().BeFalse();
    }
}
