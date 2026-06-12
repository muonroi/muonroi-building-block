namespace Muonroi.Core.Tests;

public class MDateTimeExtensionTests
{
    [Fact]
    public void ConvertTimestampToYearMonth_Returns_Correct_Value_For_Leap_Year()
    {
        DateTime dateTime = new(2024, 2, 29, 0, 0, 0, DateTimeKind.Utc);
        double timestamp = dateTime.GetTimeStamp();

        int result = timestamp.ConvertTimestampToYearMonth();

        Assert.Equal(202402, result);
    }

    [Fact]
    public void ConvertTimestampToYearMonth_Invalid_Timestamp_Throws()
    {
        Assert.Throws<OverflowException>(() => double.NaN.ConvertTimestampToYearMonth());
    }

    [Fact]
    public void ConvertTimestampToYearMonthDay_Returns_Correct_Value_For_Leap_Year()
    {
        DateTime dateTime = new(2024, 2, 29, 0, 0, 0, DateTimeKind.Utc);
        double timestamp = dateTime.GetTimeStamp();

        int result = timestamp.ConvertTimestampToYearMonthDay();

        Assert.Equal(20240229, result);
    }

    [Fact]
    public void ConvertTimestampToYearMonthDay_Invalid_Timestamp_Throws()
    {
        Assert.Throws<OverflowException>(() => double.NaN.ConvertTimestampToYearMonthDay());
    }

    [Fact]
    public void ToUtc_Converts_Local_Time()
    {
        DateTime local = new(2023, 1, 1, 12, 0, 0, DateTimeKind.Local);
        DateTime expected = local.ToUniversalTime();

        DateTime result = local.ToUtc();

        Assert.Equal(DateTimeKind.Utc, result.Kind);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ToUtc_Input_Already_Utc_Returns_Same()
    {
        DateTime utc = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        DateTime result = utc.ToUtc();

        Assert.Equal(DateTimeKind.Utc, result.Kind);
        Assert.Equal(utc, result);
    }

    [Fact]
    public void IsTheSameDate_Returns_True_For_Same_Date_Different_Time()
    {
        DateTime first = new(2024, 1, 1, 5, 0, 0, DateTimeKind.Utc);
        DateTime second = first.AddHours(3);

        Assert.True(first.IsTheSameDate(second));
    }

    [Fact]
    public void IsTheSameDate_Returns_False_For_Different_Date()
    {
        DateTime first = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime second = first.AddDays(1);

        Assert.False(first.IsTheSameDate(second));
    }

    [Fact]
    public void TimeStampToDate_Converts_Correctly()
    {
        double timestamp = DateTime.UnixEpoch.AddDays(10).Subtract(DateTime.UnixEpoch).TotalSeconds;
        DateTime expected = DateTime.UnixEpoch.AddDays(10).Date;

        DateTime actual = timestamp.TimeStampToDate();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void TimeStampToDate_Negative_Timestamp_Returns_Epoch()
    {
        DateTime actual = (-1000d).TimeStampToDate();

        Assert.Equal(DateTime.UnixEpoch.Date, actual);
    }

    [Fact]
    public void TimeStampToDateTime_Converts_Correctly()
    {
        DateTime expected = DateTimeOffset.FromUnixTimeSeconds(1000).DateTime;

        DateTime actual = 1000d.TimeStampToDateTime();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void GetTimeZoneExpiryDate_Returns_Correct_Offset()
    {
        DateTimeOffset now = new(2024, 1, 1, 10, 0, 0, TimeSpan.Zero);

        DateTimeOffset result = now.GetTimeZoneExpiryDate(7);

        Assert.Equal(new TimeSpan(7, 0, 0), result.Offset);
        Assert.Equal(now.ToOffset(new TimeSpan(7, 0, 0)).Date, result.Date);
    }

    [Fact]
    public void GetTimeZoneExpiryDate_Invalid_Zone_Throws()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        Assert.Throws<ArgumentOutOfRangeException>(() => now.GetTimeZoneExpiryDate(20));
    }

    [Fact]
    public void GreaterThanWithoutDay_Works_For_Various_Cases()
    {
        DateTime jan2024 = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime feb2024 = new(2024, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime jan2025 = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        Assert.True(jan2025.GreaterThanWithoutDay(jan2024));
        Assert.True(feb2024.GreaterThanWithoutDay(jan2024));
        Assert.False(jan2024.GreaterThanWithoutDay(feb2024));
        Assert.False(jan2024.GreaterThanWithoutDay(jan2024));
    }
}
