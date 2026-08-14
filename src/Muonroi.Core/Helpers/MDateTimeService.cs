namespace Muonroi.Core.Helpers;

/// <summary>
/// Provides helper methods for working with date and time.
/// </summary>
public class MDateTimeService : IMDateTimeService
{
    /// <summary>
    /// Gets the current local date and time.
    /// </summary>
    /// <returns>The current local date and time.</returns>
    public DateTime Now()
    {
        return DateTime.Now;
    }

    /// <summary>
    /// Gets the current UTC date and time.
    /// </summary>
    /// <returns>The current UTC date and time.</returns>
    public DateTime UtcNow()
    {
        return DateTime.UtcNow;
    }

    /// <summary>
    /// Gets the current local date.
    /// </summary>
    /// <returns>The current local date.</returns>
    public DateTime Today()
    {
        return DateTime.Today;
    }

    /// <summary>
    /// Gets the current UTC date.
    /// </summary>
    /// <returns>The current UTC date.</returns>
    public DateTime UtcToday()
    {
        return DateTime.UtcNow.Date;
    }

    /// <summary>
    /// Gets the current local date and time as a timestamp.
    /// </summary>
    /// <returns>The current local date and time as a timestamp.</returns>
    public double NowTs()
    {
        return DateTime.Now.GetTimeStamp(true);
    }

    /// <summary>
    /// Gets the current UTC date and time as a timestamp.
    /// </summary>
    /// <returns>The current UTC date and time as a timestamp.</returns>
    public double UtcNowTs()
    {
        return DateTime.UtcNow.GetTimeStamp(true);
    }
}
