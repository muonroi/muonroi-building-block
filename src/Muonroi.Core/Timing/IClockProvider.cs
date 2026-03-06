namespace Muonroi.Core.Timing;

public interface IClockProvider
{
    //
    // Summary:
    //     Gets Now.
    DateTime Now { get; }

    /// <summary>
    /// Gets UtcNow.
    /// </summary>
    DateTime UtcNow { get; }

    //
    // Summary:
    //     Gets kind.
    DateTimeKind Kind { get; }

    //
    // Summary:
    //     Is that provider supports multiple time zone.
    bool SupportsMultipleTimezone { get; }

    //
    // Summary:
    //     Normalizes given System.DateTime.
    //
    // Parameters:
    //   dateTime:
    //     DateTime to be normalized.
    //
    // Returns:
    //     Normalized DateTime
    DateTime Normalize(DateTime dateTime);
}
