namespace Muonroi.Core.Tests;

public class LocalClockProviderTests
{
    [Fact]
    public void Now_Returns_Local_Time()
    {
        LocalClockProvider provider = ClockProviders.Local;
        DateTime now = provider.Now;

        Assert.Equal(DateTimeKind.Local, now.Kind);
        Assert.InRange(now, DateTime.Now.AddSeconds(-1), DateTime.Now.AddSeconds(1));
    }

    [Fact]
    public void UtcNow_Returns_Utc_Time()
    {
        LocalClockProvider provider = ClockProviders.Local;
        DateTime utcNow = provider.UtcNow;

        Assert.Equal(DateTimeKind.Utc, utcNow.Kind);
        Assert.InRange(utcNow, DateTime.UtcNow.AddSeconds(-1), DateTime.UtcNow.AddSeconds(1));
    }

    [Fact]
    public void Kind_Returns_Local()
    {
        LocalClockProvider provider = ClockProviders.Local;

        Assert.Equal(DateTimeKind.Local, provider.Kind);
    }

    [Fact]
    public void SupportsMultipleTimezone_Is_False()
    {
        LocalClockProvider provider = ClockProviders.Local;

        Assert.False(provider.SupportsMultipleTimezone);
    }

    [Fact]
    public void Normalize_Returns_Local_Date()
    {
        LocalClockProvider provider = ClockProviders.Local;
        DateTime utc = DateTime.UtcNow;

        DateTime result = provider.Normalize(utc);

        Assert.Equal(DateTimeKind.Local, result.Kind);
        Assert.Equal(utc.ToLocalTime(), result);

        DateTime unspecified = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        DateTime unspecifiedResult = provider.Normalize(unspecified);

        Assert.Equal(DateTimeKind.Local, unspecifiedResult.Kind);
        Assert.Equal(DateTime.SpecifyKind(unspecified, DateTimeKind.Local), unspecifiedResult);
    }
}
