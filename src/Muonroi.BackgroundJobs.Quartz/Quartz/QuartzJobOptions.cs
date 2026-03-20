namespace Muonroi.BackgroundJobs.Quartz.Quartz;

/// <summary>
/// Configuration options for Quartz background jobs.
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
    public JobType JobType { get; set; } = JobType.Quartz;

    /// <summary>
    /// Gets or sets the Quartz connection string.
    /// </summary>
    public string? ConnectionString { get; set; }
}
