namespace Muonroi.BuildingBlock.Test;

public class MDateTimeServiceTests
{
    [Fact]
    public void Today_Returns_Local_Today_Date()
    {
        MDateTimeService service = new();
        DateTime expected = DateTime.Now.Date;
        Assert.Equal(expected, service.Today());
    }

    [Fact]
    public void UtcToday_Returns_Utc_Today_Date()
    {
        MDateTimeService service = new();
        DateTime expected = DateTime.UtcNow.Date;
        DateTime result = service.UtcToday();
        Assert.Equal(expected, result);
        Assert.Equal(DateTimeKind.Utc, result.Kind);
    }

    [Fact]
    public void Now_Returns_Current_Local_Time()
    {
        MDateTimeService svc = new();
        DateTime before = DateTime.Now.AddSeconds(-1);
        DateTime result = svc.Now();
        DateTime after = DateTime.Now.AddSeconds(1);
        Assert.InRange(result, before, after);
    }

    [Fact]
    public void UtcNow_Returns_Current_Utc_Time_And_Offset()
    {
        MDateTimeService svc = new();
        DateTime before = DateTime.UtcNow.AddSeconds(-1);
        DateTime result = svc.UtcNow();
        DateTime after = DateTime.UtcNow.AddSeconds(1);
        Assert.InRange(result, before, after);
        TimeSpan systemOffset = TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow);
        TimeSpan serviceOffset = svc.Now() - result;
        Assert.Equal(systemOffset.TotalMinutes, serviceOffset.TotalMinutes, 1);
    }
}
