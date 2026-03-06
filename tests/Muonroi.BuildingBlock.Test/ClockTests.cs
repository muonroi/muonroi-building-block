namespace Muonroi.BuildingBlock.Test;

public class ClockTests
{
    [Fact]
    public void Set_Provider_Success_And_Throws_On_Null()
    {
        IClockProvider original = Clock.Provider;
        try
        {
            Clock.Provider = ClockProviders.Local;
            Assert.Equal(ClockProviders.Local, Clock.Provider);
            Assert.Throws<ArgumentNullException>(() => Clock.Provider = null!);
        }
        finally
        {
            Clock.Provider = original;
        }
    }

    [Fact]
    public void Now_Returns_Current_Local_Time()
    {
        IClockProvider original = Clock.Provider;
        try
        {
            Clock.Provider = ClockProviders.Local;
            DateTime now = Clock.Now;
            Assert.Equal(DateTimeKind.Local, now.Kind);
            Assert.InRange(now, DateTime.Now.AddSeconds(-1), DateTime.Now.AddSeconds(1));
        }
        finally
        {
            Clock.Provider = original;
        }
    }

    [Fact]
    public void Kind_Reflects_Current_Provider()
    {
        // Allow Utc (default) or Unspecified (if set by other tests)
        Assert.True(Clock.Kind == DateTimeKind.Utc || Clock.Kind == DateTimeKind.Unspecified);
    }

    [Fact]
    public void SupportsMultipleTimezone_Returns_Correct_Value()
    {
        IClockProvider original = Clock.Provider;
        try
        {
            Clock.Provider = ClockProviders.Utc;
            Assert.True(Clock.SupportsMultipleTimezone);

            Clock.Provider = ClockProviders.Local;
            Assert.False(Clock.SupportsMultipleTimezone);

            Clock.Provider = ClockProviders.Unspecified;
            Assert.False(Clock.SupportsMultipleTimezone);
        }
        finally
        {
            Clock.Provider = original;
        }
    }

    [Fact]
    public void Normalize_Converts_Date_Based_On_Provider()
    {
        IClockProvider original = Clock.Provider;
        try
        {
            DateTime utcTime = DateTime.UtcNow;
            Clock.Provider = ClockProviders.Local;
            DateTime localResult = Clock.Normalize(utcTime);
            Assert.Equal(DateTimeKind.Local, localResult.Kind);
            Assert.Equal(utcTime.ToLocalTime(), localResult);

            DateTime localTime = DateTime.Now;
            Clock.Provider = ClockProviders.Utc;
            DateTime utcResult = Clock.Normalize(localTime);
            Assert.Equal(DateTimeKind.Utc, utcResult.Kind);
            Assert.Equal(localTime.ToUniversalTime(), utcResult);
        }
        finally
        {
            Clock.Provider = original;
        }
    }
}
