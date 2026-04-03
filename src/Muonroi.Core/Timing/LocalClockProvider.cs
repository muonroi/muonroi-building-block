namespace Muonroi.Core.Timing;

/// <summary>
/// Implements <see cref="IClockProvider"/> to work with local times.
/// </summary>
public class LocalClockProvider : IClockProvider
{
    /// <inheritdoc />
    public DateTime Now => DateTime.Now;

    /// <inheritdoc />
    public DateTime UtcNow => DateTime.UtcNow;

    /// <inheritdoc />
    public DateTimeKind Kind => DateTimeKind.Local;

    /// <inheritdoc />
    public bool SupportsMultipleTimezone => false;

    /// <inheritdoc />
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
