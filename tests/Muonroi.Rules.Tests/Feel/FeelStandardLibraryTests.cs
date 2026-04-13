namespace Muonroi.Rules.Tests.Feel;

public class FeelStandardLibraryTests
{
    [Fact]
    public void TryCall_ListContains_ReturnsTrue()
    {
        object?[] list = [1.0, 2.0, 3.0];
        bool found = FeelStandardLibrary.TryCall("list_contains", [list, 2.0], out object? result);
        found.Should().BeTrue();
        result.Should().Be(true);
    }

    [Fact]
    public void TryCall_Count_ReturnsListLength()
    {
        object?[] list = ["a", "b", "c"];
        FeelStandardLibrary.TryCall("count", [list], out object? result);
        result.Should().Be(3.0);
    }

    [Fact]
    public void TryCall_Min_ReturnsMinimumValue()
    {
        object?[] list = [5.0, 2.0, 8.0];
        FeelStandardLibrary.TryCall("min", [list], out object? result);
        result.Should().Be(2.0);
    }

    [Fact]
    public void TryCall_Max_ReturnsMaximumValue()
    {
        object?[] list = [5.0, 2.0, 8.0];
        FeelStandardLibrary.TryCall("max", [list], out object? result);
        result.Should().Be(8.0);
    }

    [Fact]
    public void TryCall_Sum_ReturnsSumOfElements()
    {
        object?[] list = [1.0, 2.0, 3.0];
        FeelStandardLibrary.TryCall("sum", [list], out object? result);
        result.Should().Be(6.0);
    }

    [Fact]
    public void TryCall_Mean_ReturnsAverage()
    {
        object?[] list = [2.0, 4.0, 6.0];
        FeelStandardLibrary.TryCall("mean", [list], out object? result);
        result.Should().Be(4.0);
    }

    [Fact]
    public void TryCall_UpperCase_Converts()
    {
        FeelStandardLibrary.TryCall("upper_case", ["hello"], out object? result);
        result.Should().Be("HELLO");
    }

    [Fact]
    public void TryCall_LowerCase_Converts()
    {
        FeelStandardLibrary.TryCall("lower_case", ["HELLO"], out object? result);
        result.Should().Be("hello");
    }

    [Fact]
    public void TryCall_Floor_ReturnsFlooredValue()
    {
        FeelStandardLibrary.TryCall("floor", [3.7], out object? result);
        result.Should().Be(3.0);
    }

    [Fact]
    public void TryCall_Ceiling_ReturnsCeiledValue()
    {
        FeelStandardLibrary.TryCall("ceiling", [3.2], out object? result);
        result.Should().Be(4.0);
    }

    [Fact]
    public void TryCall_Abs_ReturnsAbsoluteValue()
    {
        FeelStandardLibrary.TryCall("abs", [-5.0], out object? result);
        result.Should().Be(5.0);
    }

    [Fact]
    public void TryCall_Modulo_ReturnsRemainder()
    {
        FeelStandardLibrary.TryCall("modulo", [10.0, 3.0], out object? result);
        result.Should().Be(1.0);
    }

    [Fact]
    public void TryCall_Odd_ReturnsTrueForOddNumber()
    {
        FeelStandardLibrary.TryCall("odd", [5.0], out object? result);
        result.Should().Be(true);
    }

    [Fact]
    public void TryCall_Even_ReturnsTrueForEvenNumber()
    {
        FeelStandardLibrary.TryCall("even", [4.0], out object? result);
        result.Should().Be(true);
    }

    [Fact]
    public void TryCall_Not_NegatesBoolean()
    {
        FeelStandardLibrary.TryCall("not", [true], out object? result);
        result.Should().Be(false);
    }

    [Fact]
    public void TryCall_UnknownFunction_ReturnsFalse()
    {
        bool found = FeelStandardLibrary.TryCall("nonexistent_function", [], out object? result);
        found.Should().BeFalse();
        result.Should().BeNull();
    }

    [Fact]
    public void TryCall_Sqrt_ReturnsSquareRoot()
    {
        FeelStandardLibrary.TryCall("sqrt", [16.0], out object? result);
        result.Should().Be(4.0);
    }

    [Fact]
    public void TryCall_Decimal_RoundsToScale()
    {
        FeelStandardLibrary.TryCall("decimal", [3.14159, 2.0], out object? result);
        result.Should().Be(3.14);
    }

    [Fact]
    public void TryCall_Reverse_ReversesListOrder()
    {
        object?[] list = [1.0, 2.0, 3.0];
        FeelStandardLibrary.TryCall("reverse", [list], out object? result);
        (result as object?[]).Should().BeEquivalentTo(new object[] { 3.0, 2.0, 1.0 });
    }

    [Fact]
    public void TryCall_Duration_ParsesIsoDuration()
    {
        FeelStandardLibrary.TryCall("duration", ["PT2H30M"], out object? result);
        result.Should().Be(TimeSpan.FromHours(2).Add(TimeSpan.FromMinutes(30)));
    }

    [Fact]
    public void TryCall_Date_ParsesDateString()
    {
        FeelStandardLibrary.TryCall("date", ["2024-01-15"], out object? result);
        result.Should().Be(new DateOnly(2024, 1, 15));
    }
}
