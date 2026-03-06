namespace Muonroi.BackgroundJobs.Quartz.Quartz;

public static class BackgroundJobHandler
{
    public static IServiceCollection AddBackgroundJobs(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        BackgroundJobConfigs cfg = new();
        configuration.GetSection(BackgroundJobConfigs.SectionName).Bind(cfg);

        if (cfg.JobType != JobType.Quartz)
        {
            throw new InvalidOperationException(
                $"Invalid JobType '{cfg.JobType}' for package '{nameof(BackgroundJobs.Quartz)}'.");
        }

        services.AddQuartz(cfg =>
        {
            cfg.AddJobListener<QuartzContextJobListener>();
        });
        services.AddSingleton<QuartzContextJobListener>();
        services.AddQuartzHostedService(o => o.WaitForJobsToComplete = true);

        return services;
    }
}
