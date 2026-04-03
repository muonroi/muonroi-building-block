namespace Muonroi.Core.Timing;

/// <summary>
/// Provides default instances of clock providers.
/// </summary>
public static class ClockProviders
{
    /// <summary>
    /// Gets the <see cref="UnspecifiedClockProvider"/> instance.
    /// </summary>
    public static UnspecifiedClockProvider Unspecified { get; } = new UnspecifiedClockProvider();

    /// <summary>
    /// Gets the <see cref="LocalClockProvider"/> instance.
    /// </summary>
    public static LocalClockProvider Local { get; } = new LocalClockProvider();

    /// <summary>
    /// Gets the <see cref="UtcClockProvider"/> instance.
    /// </summary>
    public static UtcClockProvider Utc { get; } = new UtcClockProvider();
}
