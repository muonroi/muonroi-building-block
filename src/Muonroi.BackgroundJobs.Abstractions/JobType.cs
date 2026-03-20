namespace Muonroi.BackgroundJobs.Abstractions;

/// <summary>
/// Supported background job providers.
/// </summary>
public enum JobType
{
    /// <summary>
    /// Hangfire provider.
    /// </summary>
    Hangfire,
    /// <summary>
    /// Quartz provider.
    /// </summary>
    Quartz
}
