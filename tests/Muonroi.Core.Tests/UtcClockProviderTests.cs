namespace Muonroi.Core.Tests;

public class UtcClockProviderTests
{
    [Fact]
    public void Now_Returns_Current_Local_Time()
    {
        UtcClockProvider provider = ClockProviders.Utc;
        DateTime before = DateTime.Now.AddSeconds(-1);
        DateTime value = provider.Now;
        DateTime after = DateTime.Now.AddSeconds(1);

        Assert.InRange(value, before, after);
        Assert.Equal(DateTimeKind.Local, value.Kind);
    }

    [Fact]
    public void UtcNow_Returns_Current_Utc_Time()
    {
        UtcClockProvider provider = ClockProviders.Utc;
        DateTime before = DateTime.UtcNow.AddSeconds(-1);
        DateTime value = provider.UtcNow;
        DateTime after = DateTime.UtcNow.AddSeconds(1);

        Assert.InRange(value, before, after);
        Assert.Equal(DateTimeKind.Utc, value.Kind);
    }

    [Fact]
    public void Kind_Returns_Utc()
    {
        Assert.Equal(DateTimeKind.Utc, ClockProviders.Utc.Kind);
    }

    [Fact]
    public void SupportsMultipleTimezone_Is_True()
    {
        Assert.True(ClockProviders.Utc.SupportsMultipleTimezone);
    }

    [Theory]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Utc)]
    [InlineData(DateTimeKind.Unspecified)]
    public void Normalize_Converts_To_Utc(DateTimeKind kind)
    {
        DateTime value = new(2024, 1, 1, 12, 0, 0, kind);

        DateTime result = ClockProviders.Utc.Normalize(value);

        if (kind == DateTimeKind.Local)
        {
            Assert.Equal(value.ToUniversalTime(), result);
            return;
        }

        if (kind == DateTimeKind.Unspecified)
        {
            Assert.Equal(value.Ticks, result.Ticks);
            Assert.Equal(DateTimeKind.Utc, result.Kind);
            return;
        }

        Assert.Equal(value, result);
    }
}
