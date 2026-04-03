namespace Muonroi.Core.Timing;

/// <summary>
/// Defines the interface for a clock provider.
/// </summary>
public interface IClockProvider
{
    /// <summary>
    /// Gets the current date and time.
    /// </summary>
    DateTime Now { get; }

    /// <summary>
    /// Gets the current UTC date and time.
    /// </summary>
    DateTime UtcNow { get; }

    /// <summary>
    /// Gets the <see cref="DateTimeKind"/> supported by this provider.
    /// </summary>
    DateTimeKind Kind { get; }

    /// <summary>
    /// Gets a value indicating whether this provider supports multiple time zones.
    /// </summary>
    bool SupportsMultipleTimezone { get; }

    /// <summary>
    /// Normalizes the specified <see cref="DateTime"/>.
    /// </summary>
    /// <param name="dateTime">The <see cref="DateTime"/> to be normalized.</param>
    /// <returns>The normalized <see cref="DateTime"/>.</returns>
    DateTime Normalize(DateTime dateTime);
}
