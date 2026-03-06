using Hangfire;
using Hangfire.Common;
using Hangfire.Storage;
using Quartz;

namespace Muonroi.BuildingBlock.Test;

public class BackgroundJobHandlerTests
{
    private static IConfiguration CreateConfiguration(JobType jobType)
    {
        Dictionary<string, string?> values = new()
        {
            [$"{BackgroundJobConfigs.SectionName}:JobType"] = jobType.ToString()
        };

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    [Fact]
    public void AddBackgroundJobs_Hangfire_ConfiguresAutomaticRetryFilter()
    {
        IConfiguration configuration = CreateConfiguration(JobType.Hangfire);
        ServiceCollection services = [];
        services.AddOptions();
        services.AddLogging();

        JobStorage.Current = new StubJobStorage();
        List<JobFilter> originalFilters = [.. GlobalJobFilters.Filters];
        GlobalJobFilters.Filters.Clear();
        try
        {
            using ServiceProvider provider = services.AddBackgroundJobs(configuration).BuildServiceProvider();
            _ = provider.GetRequiredService<IGlobalConfiguration>();
            Assert.Null(provider.GetService<ISchedulerFactory>());

            AutomaticRetryAttribute[] retries = [.. GlobalJobFilters.Filters
                .Select(f => f.Instance)
                .OfType<AutomaticRetryAttribute>()];

            Assert.Contains(retries, attr => attr.Attempts == 3);
            Assert.Contains(retries,
                attr => attr.DelaysInSeconds is { Length: 3 } delays && delays.SequenceEqual([5, 10, 30]));
        }
        finally
        {
            GlobalJobFilters.Filters.Clear();
            foreach (JobFilter filter in originalFilters) GlobalJobFilters.Filters.Add(filter.Instance);
            JobStorage.Current = null;
        }
    }

    [Fact]
    public void AddBackgroundJobs_Quartz_WaitsForJobsToComplete()
    {
        IConfiguration configuration = CreateConfiguration(JobType.Quartz);
        ServiceCollection services = [];

        services.AddBackgroundJobs(configuration);
        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<ISchedulerFactory>());
        Assert.Null(provider.GetService<IGlobalConfiguration>());

        QuartzHostedServiceOptions options = provider
            .GetRequiredService<IOptions<QuartzHostedServiceOptions>>().Value;

        Assert.True(options.WaitForJobsToComplete);
    }
}

internal sealed class StubJobStorage : JobStorage
{
    public override IStorageConnection GetConnection()
    {
        throw new NotSupportedException();
    }

    public override IMonitoringApi GetMonitoringApi()
    {
        throw new NotSupportedException();
    }
}
