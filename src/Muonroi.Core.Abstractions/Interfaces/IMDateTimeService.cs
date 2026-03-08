namespace Muonroi.Core.Abstractions.Interfaces;

/// <summary>
/// Provides date and time services.
/// </summary>
public interface IMDateTimeService
{
    /// <summary>
    /// Gets the current date and time on this computer, expressed as the local time.
    /// </summary>
    /// <returns>The current local date and time.</returns>
    DateTime Now();

    /// <summary>
    /// Gets the current date and time on this computer, expressed as the Coordinated Universal Time (UTC).
    /// </summary>
    /// <returns>The current UTC date and time.</returns>
    DateTime UtcNow();

    /// <summary>
    /// Gets the current date.
    /// </summary>
    /// <returns>The current date.</returns>
    DateTime Today();

    /// <summary>
    /// Gets the current date in UTC.
    /// </summary>
    /// <returns>The current UTC date.</returns>
    DateTime UtcToday();

    /// <summary>
    /// Gets the current timestamp in seconds.
    /// </summary>
    /// <returns>The current timestamp.</returns>
    double NowTs();

    /// <summary>
    /// Gets the current UTC timestamp in seconds.
    /// </summary>
    /// <returns>The current UTC timestamp.</returns>
    double UtcNowTs();
}
