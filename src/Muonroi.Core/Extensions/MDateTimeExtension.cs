namespace Muonroi.Core.Extensions;

/// <summary>
/// Provides extension methods for <see cref="DateTime"/> and <see cref="DateTimeOffset"/>.
/// </summary>
public static class MDateTimeExtension
{
    /// <summary>
    /// Checks if two dates are the same (year, month, and day).
    /// </summary>
    /// <param name="srcDate">The source date.</param>
    /// <param name="desDate">The destination date.</param>
    /// <returns>True if they are the same date; otherwise, false.</returns>
    public static bool IsTheSameDate(this DateTime srcDate, DateTime desDate)
    {
        return srcDate.Year == desDate.Year && srcDate.Month == desDate.Month && srcDate.Day == desDate.Day;
    }

    /// <summary>
    /// Converts a Unix timestamp to a <see cref="DateTime"/> representing the date only.
    /// </summary>
    /// <param name="timeStamp">The Unix timestamp.</param>
    /// <returns>A <see cref="DateTime"/> representing the date.</returns>
    public static DateTime TimeStampToDate(this double timeStamp)
    {
        timeStamp = timeStamp < 0.0 ? 0.0 : timeStamp;
        return timeStamp.TimeStampToDateTime().Date;
    }

    /// <summary>
    /// Converts a Unix timestamp to a <see cref="DateTime"/>.
    /// </summary>
    /// <param name="timeStamp">The Unix timestamp.</param>
    /// <returns>A <see cref="DateTime"/> representation of the timestamp.</returns>
    public static DateTime TimeStampToDateTime(this double timeStamp)
    {
        timeStamp = timeStamp < 0.0 ? 0.0 : timeStamp;
        return DateTimeOffset.FromUnixTimeSeconds(Convert.ToInt64(timeStamp)).DateTime;
    }

    /// <summary>
    /// Gets the Unix timestamp for the specified <see cref="DateTime"/>.
    /// </summary>
    /// <param name="dateTime">The <see cref="DateTime"/> to convert.</param>
    /// <param name="includedTimeValue">True to include time in the calculation; otherwise, false.</param>
    /// <returns>The Unix timestamp.</returns>
    public static double GetTimeStamp(this DateTime dateTime, bool includedTimeValue = false)
    {
        return Math.Round((includedTimeValue ? dateTime : dateTime.Date).Subtract(DateTime.UnixEpoch).TotalSeconds, 0);
    }

    /// <summary>
    /// Gets a <see cref="DateTimeOffset"/> representing the expiry date in a specific time zone.
    /// </summary>
    /// <param name="dateTimeOffset">The source <see cref="DateTimeOffset"/>.</param>
    /// <param name="zoneHour">The time zone offset in hours.</param>
    /// <returns>A <see cref="DateTimeOffset"/> representing the start of the date in the specified time zone.</returns>
    public static DateTimeOffset GetTimeZoneExpiryDate(this DateTimeOffset dateTimeOffset, int zoneHour)
    {
        return new DateTimeOffset(dateTimeOffset.ToOffset(new TimeSpan(zoneHour, 0, 0)).Date,
            new TimeSpan(zoneHour, 0, 0));
    }

    /// <summary>
    /// Checks if the source date is greater than the target date, ignoring the day part.
    /// </summary>
    /// <param name="dtFrom">The source date.</param>
    /// <param name="dtTo">The target date.</param>
    /// <returns>True if the source date is later than the target date (month/year); otherwise, false.</returns>
    public static bool GreaterThanWithoutDay(this DateTime dtFrom, DateTime dtTo)
    {
        return dtFrom.Year > dtTo.Year || (dtFrom.Year == dtTo.Year && dtFrom.Month > dtTo.Month);
    }

    /// <summary>
    /// Converts a Unix timestamp to an integer representing Year and Month (YYYYMM).
    /// </summary>
    /// <param name="timeStamp">The Unix timestamp.</param>
    /// <returns>An integer in YYYYMM format.</returns>
    public static int ConvertTimestampToYearMonth(this double timeStamp)
    {
        DateTime dateTime = timeStamp.TimeStampToDate();
        string text = dateTime.Month < 10 ? "0" + dateTime.Month : dateTime.Month.ToString();
        string s = dateTime.Year + text;
        return int.Parse(s);
    }

    /// <summary>
    /// Converts a Unix timestamp to an integer representing Year, Month, and Day (YYYYMMDD).
    /// </summary>
    /// <param name="timeStamp">The Unix timestamp.</param>
    /// <returns>An integer in YYYYMMDD format.</returns>
    public static int ConvertTimestampToYearMonthDay(this double timeStamp)
    {
        DateTime dateTime = timeStamp.TimeStampToDate();
        string text = dateTime.Month < 10 ? "0" + dateTime.Month : dateTime.Month.ToString();
        string text2 = dateTime.Day < 10 ? "0" + dateTime.Day : dateTime.Day.ToString();
        string s = dateTime.Year + text + text2;
        return int.Parse(s);
    }

    /// <summary>
    /// Converts a <see cref="DateTime"/> to UTC.
    /// </summary>
    /// <param name="dateTime">The <see cref="DateTime"/> to convert.</param>
    /// <returns>The UTC representation of the date and time.</returns>
    public static DateTime ToUtc(this DateTime dateTime)
    {
        if (dateTime.Kind == DateTimeKind.Utc)
        {
            return dateTime;
        }

        DateTime localTime = DateTime.SpecifyKind(dateTime, DateTimeKind.Local);
        return localTime.ToUniversalTime();
    }
}
