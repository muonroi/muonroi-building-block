using Muonroi.Core.Extensions;

namespace Muonroi.Core.Helpers;

public class MDateTimeService : IMDateTimeService
{
    public DateTime Now()
    {
        return DateTime.Now;
    }

    public DateTime UtcNow()
    {
        return DateTime.UtcNow;
    }

    public DateTime Today()
    {
        return DateTime.Today;
    }

    public DateTime UtcToday()
    {
        return DateTime.UtcNow.Date;
    }

    public double NowTs()
    {
        return DateTime.Now.GetTimeStamp(true);
    }

    public double UtcNowTs()
    {
        return DateTime.UtcNow.GetTimeStamp(true);
    }
}
