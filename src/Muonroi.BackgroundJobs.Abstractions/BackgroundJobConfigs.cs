namespace Muonroi.BackgroundJobs.Abstractions;

/// <summary>
/// Configuration options for background job providers.
/// </summary>
public class BackgroundJobConfigs
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "BackgroundJobConfigs";

    /// <summary>
    /// Gets or sets the job provider type.
    /// </summary>
    public JobType JobType { get; set; } = JobType.Hangfire;

    /// <summary>
    /// Gets or sets the provider connection string.
    /// </summary>
    public string? ConnectionString { get; set; }
}
