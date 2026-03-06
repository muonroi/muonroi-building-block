namespace Muonroi.Core.Timing;

public class UnspecifiedClockProvider : IClockProvider
{
    public DateTime Now => DateTime.Now;
    public DateTime UtcNow => DateTime.UtcNow;


    public DateTimeKind Kind => DateTimeKind.Unspecified;

    public bool SupportsMultipleTimezone => false;

    public DateTime Normalize(DateTime dateTime)
    {
        return dateTime;
    }

    internal UnspecifiedClockProvider()
    {
    }
}
