namespace Muonroi.Core.Timing;

/// <summary>
/// Implements <see cref="IClockProvider"/> to work with UTC times.
/// </summary>
public class UtcClockProvider : IClockProvider
{
    /// <inheritdoc />
    public DateTime Now => DateTime.Now;

    /// <inheritdoc />
    public DateTime UtcNow => DateTime.UtcNow;

    /// <inheritdoc />
    public DateTimeKind Kind => DateTimeKind.Utc;

    /// <inheritdoc />
    public bool SupportsMultipleTimezone => true;

    /// <inheritdoc />
    public DateTime Normalize(DateTime dateTime)
    {
        if (dateTime.Kind == DateTimeKind.Local)
        {
            return dateTime.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)
            : dateTime.ToUniversalTime();
        }
        else
        {
            return dateTime.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)
            : dateTime;
        }
    }

    internal UtcClockProvider()
    {
    }
}
