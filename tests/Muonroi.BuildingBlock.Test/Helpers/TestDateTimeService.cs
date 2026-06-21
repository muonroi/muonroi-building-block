namespace Muonroi.BuildingBlock.Test.Helpers;

internal sealed class TestDateTimeService : IMDateTimeService
{
    public DateTime Now() => DateTime.Now;
    public DateTime UtcNow() => DateTime.UtcNow;
    public DateTime Today() => DateTime.Today;
    public DateTime UtcToday() => DateTime.UtcNow.Date;
    public double NowTs() => DateTimeOffset.Now.ToUnixTimeSeconds();
    public double UtcNowTs() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
}