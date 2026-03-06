namespace Muonroi.Core.Extensions;

public static class MDateTimeExtension
{
    public static bool IsTheSameDate(this DateTime srcDate, DateTime desDate)
    {
        return srcDate.Year == desDate.Year && srcDate.Month == desDate.Month && srcDate.Day == desDate.Day;
    }

    public static DateTime TimeStampToDate(this double timeStamp)
    {
        timeStamp = timeStamp < 0.0 ? 0.0 : timeStamp;
        return timeStamp.TimeStampToDateTime().Date;
    }

    public static DateTime TimeStampToDateTime(this double timeStamp)
    {
        timeStamp = timeStamp < 0.0 ? 0.0 : timeStamp;
        return DateTimeOffset.FromUnixTimeSeconds(Convert.ToInt64(timeStamp)).DateTime;
    }

    public static double GetTimeStamp(this DateTime dateTime, bool includedTimeValue = false)
    {
        return Math.Round((includedTimeValue ? dateTime : dateTime.Date).Subtract(DateTime.UnixEpoch).TotalSeconds, 0);
    }

    public static DateTimeOffset GetTimeZoneExpiryDate(this DateTimeOffset dateTimeOffset, int zoneHour)
    {
        return new DateTimeOffset(dateTimeOffset.ToOffset(new TimeSpan(zoneHour, 0, 0)).Date,
            new TimeSpan(zoneHour, 0, 0));
    }

    public static bool GreaterThanWithoutDay(this DateTime dtFrom, DateTime dtTo)
    {
        return dtFrom.Year > dtTo.Year || (dtFrom.Year == dtTo.Year && dtFrom.Month > dtTo.Month);
    }

    public static int ConvertTimestampToYearMonth(this double timeStamp)
    {
        DateTime dateTime = timeStamp.TimeStampToDate();
        string text = dateTime.Month < 10 ? "0" + dateTime.Month : dateTime.Month.ToString();
        string s = dateTime.Year + text;
        return int.Parse(s);
    }

    public static int ConvertTimestampToYearMonthDay(this double timeStamp)
    {
        DateTime dateTime = timeStamp.TimeStampToDate();
        string text = dateTime.Month < 10 ? "0" + dateTime.Month : dateTime.Month.ToString();
        string text2 = dateTime.Day < 10 ? "0" + dateTime.Day : dateTime.Day.ToString();
        string s = dateTime.Year + text + text2;
        return int.Parse(s);
    }

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
