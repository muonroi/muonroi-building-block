namespace Muonroi.BackgroundJobs.Quartz.Quartz;

/// <summary>
/// Registers Quartz as the background job provider.
/// </summary>
public static class BackgroundJobHandler
{
    /// <summary>
    /// Adds Quartz background job services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration root.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddBackgroundJobs(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        BackgroundJobConfigs cfg = new();
        configuration.GetSection(BackgroundJobConfigs.SectionName).Bind(cfg);

        MGuard.Configured(cfg.JobType == JobType.Quartz,
                $"Invalid JobType '{cfg.JobType}' for package '{nameof(BackgroundJobs.Quartz)}'.",
                "BackgroundJobConfigs:JobType");

        services.AddQuartz(q =>
        {
            // The listener will handle context restoration for all jobs
            // Note: Listeners are registered globally in the scheduler
        });
        
        services.AddSingleton<QuartzContextJobListener>();
        services.AddQuartzHostedService(o => o.WaitForJobsToComplete = true);

        services.TryAddScoped<IBackgroundJobScheduler, QuartzJobScheduler>();

        return services;
    }
}
