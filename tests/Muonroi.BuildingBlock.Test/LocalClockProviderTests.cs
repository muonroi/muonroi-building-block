namespace Muonroi.BuildingBlock.Test;

public class LocalClockProviderTests
{
    [Fact]
    public void Now_Returns_Local_Time()
    {
        LocalClockProvider provider = new();
        DateTime now = provider.Now;
        Assert.Equal(DateTimeKind.Local, now.Kind);
        Assert.InRange(now, DateTime.Now.AddSeconds(-1), DateTime.Now.AddSeconds(1));
    }

    [Fact]
    public void UtcNow_Returns_Utc_Time()
    {
        LocalClockProvider provider = new();
        DateTime utcNow = provider.UtcNow;
        Assert.Equal(DateTimeKind.Utc, utcNow.Kind);
        Assert.InRange(utcNow, DateTime.UtcNow.AddSeconds(-1), DateTime.UtcNow.AddSeconds(1));
    }

    [Fact]
    public void Kind_Returns_Local()
    {
        LocalClockProvider provider = new();
        Assert.Equal(DateTimeKind.Local, provider.Kind);
    }

    [Fact]
    public void SupportsMultipleTimezone_Is_False()
    {
        LocalClockProvider provider = new();
        Assert.False(provider.SupportsMultipleTimezone);
    }

    [Fact]
    public void Normalize_Returns_Local_Date()
    {
        LocalClockProvider provider = new();
        DateTime utc = DateTime.UtcNow;
        DateTime result = provider.Normalize(utc);
        Assert.Equal(DateTimeKind.Local, result.Kind);
        Assert.Equal(utc.ToLocalTime(), result);

        DateTime unspecified = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        DateTime result2 = provider.Normalize(unspecified);
        Assert.Equal(DateTimeKind.Local, result2.Kind);
        Assert.Equal(DateTime.SpecifyKind(unspecified, DateTimeKind.Local), result2);
    }
}
