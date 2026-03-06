namespace Muonroi.BackgroundJobs.Hangfire.Hangfire;

public class BackgroundJobConfigs
{
    public const string SectionName = "BackgroundJobConfigs";
    public JobType JobType { get; set; } = JobType.Hangfire;
    public string? ConnectionString { get; set; }
}
