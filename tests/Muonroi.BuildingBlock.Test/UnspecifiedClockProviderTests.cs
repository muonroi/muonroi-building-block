namespace Muonroi.BuildingBlock.Test;

public class UnspecifiedClockProviderTests
{
    [Fact]
    public void Now_Returns_Current_Local_Time()
    {
        UnspecifiedClockProvider provider = ClockProviders.Unspecified;
        DateTime before = DateTime.Now.AddSeconds(-1);
        DateTime value = provider.Now;
        DateTime after = DateTime.Now.AddSeconds(1);

        Assert.InRange(value, before, after);
        Assert.Equal(DateTimeKind.Local, value.Kind);
        Assert.Equal(DateTimeKind.Unspecified, provider.Kind);
    }

    [Fact]
    public void Kind_Returns_Unspecified()
    {
        Assert.Equal(DateTimeKind.Unspecified, ClockProviders.Unspecified.Kind);
    }

    [Fact]
    public void SupportsMultipleTimezone_IsFalse()
    {
        Assert.False(ClockProviders.Unspecified.SupportsMultipleTimezone);
    }

    [Theory]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Utc)]
    [InlineData(DateTimeKind.Unspecified)]
    public void Normalize_Returns_Same_Value(DateTimeKind kind)
    {
        DateTime dt = new(2024, 1, 1, 0, 0, 0, kind);
        DateTime result = ClockProviders.Unspecified.Normalize(dt);
        Assert.Equal(dt, result);
        Assert.Equal(kind, result.Kind);
    }
}
