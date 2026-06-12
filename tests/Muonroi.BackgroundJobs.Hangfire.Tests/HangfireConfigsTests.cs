using Muonroi.Core.Abstractions.Exceptions;
namespace Muonroi.BackgroundJobs.Hangfire.Tests;

using HangfireConfigs = Hangfire.BackgroundJobConfigs;
using HangfireHandler = Hangfire.BackgroundJobHandler;
using Muonroi.BackgroundJobs.Abstractions;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public class HangfireConfigsTests
{
    [Fact]
    public void SectionName_Should_Be_BackgroundJobConfigs()
    {
        HangfireConfigs.SectionName.Should().Be("BackgroundJobConfigs");
    }

    [Fact]
    public void Default_JobType_Should_Be_Hangfire()
    {
        HangfireConfigs cfg = new();
        cfg.JobType.Should().Be(JobType.Hangfire);
    }

    [Fact]
    public void AddBackgroundJobs_With_Wrong_JobType_Should_Throw()
    {
        ServiceCollection services = new();
        Dictionary<string, string?> configDict = new()
        {
            ["BackgroundJobConfigs:JobType"] = "Quartz"
        };
        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(configDict).Build();

        Action act = () => HangfireHandler.AddBackgroundJobs(services, config);

        act.Should().Throw<MConfigurationException>()
            .WithMessage("*Invalid JobType*Quartz*Hangfire*");
    }

    [Fact]
    public void ConnectionString_Should_Be_Settable()
    {
        HangfireConfigs cfg = new()
        {
            ConnectionString = "redis://localhost"
        };

        cfg.ConnectionString.Should().Be("redis://localhost");
    }
}
