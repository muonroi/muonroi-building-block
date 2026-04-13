using Muonroi.Core.Abstractions.Exceptions;
namespace Muonroi.BuildingBlock.Test;

public class MDateTimeExtensionTests
{
    [Fact]
    public void ConvertTimestampToYearMonth_Returns_Correct_Value_For_Leap_Year()
    {
        DateTime dt = new(2024, 2, 29, 0, 0, 0, DateTimeKind.Utc);
        double ts = dt.GetTimeStamp();
        int result = ts.ConvertTimestampToYearMonth();
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
        DateTime dt = new(2024, 2, 29, 0, 0, 0, DateTimeKind.Utc);
        double ts = dt.GetTimeStamp();
        int result = ts.ConvertTimestampToYearMonthDay();
        Assert.Equal(20240229, result);
    }

    [Fact]
    public void ConvertTimestampToYearMonthDay_Invalid_Timestamp_Throws()
    {
        Assert.Throws<OverflowException>(() => double.NaN.ConvertTimestampToYearMonthDay());
    }

    [Fact]
    public void ToUTC_Converts_Local_Time()
    {
        DateTime local = new(2023, 1, 1, 12, 0, 0, DateTimeKind.Local);
        DateTime expected = local.ToUniversalTime();
        DateTime result = local.ToUtc();
        Assert.Equal(DateTimeKind.Utc, result.Kind);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ToUTC_Input_Already_Utc_Returns_Same()
    {
        DateTime utc = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime result = utc.ToUtc();
        Assert.Equal(DateTimeKind.Utc, result.Kind);
        Assert.Equal(utc, result);
    }

    [Fact]
    public void IsTheSameDate_ReturnsTrue_ForSameDateDifferentTime()
    {
        DateTime dt1 = new(2024, 1, 1, 5, 0, 0, DateTimeKind.Utc);
        DateTime dt2 = dt1.AddHours(3);
        Assert.True(dt1.IsTheSameDate(dt2));
    }

    [Fact]
    public void IsTheSameDate_ReturnsFalse_ForDifferentDate()
    {
        DateTime dt1 = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime dt2 = dt1.AddDays(1);
        Assert.False(dt1.IsTheSameDate(dt2));
    }

    [Fact]
    public void IsTheSameDate_DefaultDates()
    {
        DateTime dt1 = default;
        DateTime dt2 = default;
        Assert.True(dt1.IsTheSameDate(dt2));
    }

    [Fact]
    public void TimeStampToDate_ConvertsCorrectly()
    {
        double ts = DateTime.UnixEpoch.AddDays(10).Subtract(DateTime.UnixEpoch).TotalSeconds;
        DateTime expected = DateTime.UnixEpoch.AddDays(10).Date;
        DateTime actual = ts.TimeStampToDate();
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void TimeStampToDate_NegativeTimestamp_ReturnsEpoch()
    {
        double ts = -1000;
        DateTime actual = ts.TimeStampToDate();
        Assert.Equal(DateTime.UnixEpoch.Date, actual);
    }

    [Fact]
    public void TimeStampToDateTime_ConvertsCorrectly()
    {
        double ts = 1000;
        DateTime expected = DateTimeOffset.FromUnixTimeSeconds(1000).DateTime;
        DateTime actual = ts.TimeStampToDateTime();
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void GetTimeZoneExpiryDate_ReturnsCorrectOffset()
    {
        DateTimeOffset now = new(2024, 1, 1, 10, 0, 0, TimeSpan.Zero);
        DateTimeOffset result = now.GetTimeZoneExpiryDate(7);
        Assert.Equal(new TimeSpan(7, 0, 0), result.Offset);
        Assert.Equal(now.ToOffset(new TimeSpan(7, 0, 0)).Date, result.Date);
    }

    [Fact]
    public void GetTimeZoneExpiryDate_InvalidZone_Throws()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Assert.Throws<MArgumentException>(() => now.GetTimeZoneExpiryDate(20));
    }

    [Fact]
    public void GreaterThanWithoutDay_WorksForVariousCases()
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
