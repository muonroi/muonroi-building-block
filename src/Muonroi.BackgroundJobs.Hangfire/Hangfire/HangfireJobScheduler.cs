namespace Muonroi.BackgroundJobs.Hangfire.Hangfire;

public static class BackgroundJobHandler
{
    public static IServiceCollection AddBackgroundJobs(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        BackgroundJobConfigs cfg = new();
        configuration.GetSection(BackgroundJobConfigs.SectionName).Bind(cfg);

        if (cfg.JobType != JobType.Hangfire)
        {
            throw new InvalidOperationException(
                $"Invalid JobType '{cfg.JobType}' for package '{nameof(BackgroundJobs.Hangfire)}'.");
        }

        services.AddSingleton<JobContextActivatorFilter>();
        services.AddHangfire((serviceProvider, x) =>
        {
            x.UseSimpleAssemblyNameTypeSerializer();
            x.UseRecommendedSerializerSettings();
            AutomaticRetryAttribute filter = new()
            {
                Attempts = 3,
                DelaysInSeconds = [5, 10, 30]
            };
            x.UseFilter(filter);
            x.UseFilter(serviceProvider.GetRequiredService<JobContextActivatorFilter>());
        });
        services.AddHangfireServer();

        return services;
    }
}
