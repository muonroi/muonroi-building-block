namespace Muonroi.BackgroundJobs.Quartz.Quartz;

public class BackgroundJobConfigs
{
    public const string SectionName = "BackgroundJobConfigs";
    public JobType JobType { get; set; } = JobType.Quartz;
    public string? ConnectionString { get; set; }
}
