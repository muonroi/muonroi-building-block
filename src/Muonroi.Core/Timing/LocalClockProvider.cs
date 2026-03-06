namespace Muonroi.Core.Timing;

/// <summary>
/// Implements <see cref="IClockProvider"/> to work with local times.
/// </summary>
public class LocalClockProvider : IClockProvider
{
    public DateTime Now => DateTime.Now;

    public DateTime UtcNow => DateTime.UtcNow;


    public DateTimeKind Kind => DateTimeKind.Local;

    public bool SupportsMultipleTimezone => false;

    public DateTime Normalize(DateTime dateTime)
    {
        if (dateTime.Kind == DateTimeKind.Utc)
        {
            return dateTime.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(dateTime, DateTimeKind.Local)
            : dateTime.ToLocalTime();
        }
        else
        {
            return dateTime.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(dateTime, DateTimeKind.Local)
            : dateTime;
        }
    }

    internal LocalClockProvider()
    {
    }
}
