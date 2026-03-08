namespace Muonroi.Core.Timing;

/// <summary>
/// Implements <see cref="IClockProvider"/> to work with unspecified times.
/// </summary>
public class UnspecifiedClockProvider : IClockProvider
{
    /// <inheritdoc />
    public DateTime Now => DateTime.Now;

    /// <inheritdoc />
    public DateTime UtcNow => DateTime.UtcNow;

    /// <inheritdoc />
    public DateTimeKind Kind => DateTimeKind.Unspecified;

    /// <inheritdoc />
    public bool SupportsMultipleTimezone => false;

    /// <inheritdoc />
    public DateTime Normalize(DateTime dateTime)
    {
        return dateTime;
    }

    internal UnspecifiedClockProvider()
    {
    }
}
