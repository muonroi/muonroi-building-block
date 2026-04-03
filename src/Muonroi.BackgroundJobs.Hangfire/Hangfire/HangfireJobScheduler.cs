using Hangfire;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Muonroi.BackgroundJobs.Abstractions;
using Muonroi.Core.Abstractions.Exceptions;

namespace Muonroi.BackgroundJobs.Hangfire.Hangfire;

/// <summary>
/// Registers Hangfire as the background job provider.
/// </summary>
public static class BackgroundJobHandler
{
    /// <summary>
    /// Adds Hangfire background job services.
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

        if (cfg.JobType != JobType.Hangfire)
        {
            throw new MConfigurationException(
                $"Invalid JobType '{cfg.JobType}' for package '{nameof(BackgroundJobs.Hangfire)}'.", "BackgroundJobs:JobType");
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

        services.TryAddScoped<IBackgroundJobScheduler, HangfireJobScheduler>();

        return services;
    }
}
