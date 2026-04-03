using Muonroi.Core.Abstractions.Guards;

namespace Muonroi.Core.Timing;

/// <summary>
/// Used to perform some common date-time operations.
/// </summary>
public static class Clock
{
    /// <summary>
    /// This object is used to perform all <see cref="Clock"/> operations.
    /// Default value: <see cref="UnspecifiedClockProvider"/>.
    /// </summary>
    public static IClockProvider Provider
    {
        get => _provider;
        set => _provider = MGuard.NotNull(value);
    }

    private static IClockProvider _provider = ClockProviders.Unspecified;

    static Clock()
    {
    }

    /// <summary>
    /// Gets Now using current <see cref="Provider"/>.
    /// </summary>
    public static DateTime Now => Provider.Now;

    /// <summary>
    /// Gets UtcNow using current <see cref="Provider"/>.
    /// </summary>
    public static DateTime UtcNow => Provider.UtcNow;

    /// <summary>
    /// Gets the <see cref="DateTimeKind"/> supported by the current <see cref="Provider"/>.
    /// </summary>
    public static DateTimeKind Kind => Provider.Kind;

    /// <summary>
    /// Returns true if multiple timezone is supported, returns false if not.
    /// </summary>
    public static bool SupportsMultipleTimezone => Provider.SupportsMultipleTimezone;

    /// <summary>
    /// Normalizes given <see cref="DateTime"/> using current <see cref="Provider"/>.
    /// </summary>
    /// <param name="dateTime">DateTime to be normalized.</param>
    /// <returns>Normalized DateTime</returns>
    public static DateTime Normalize(DateTime dateTime)
    {
        return Provider.Normalize(dateTime);
    }
}
