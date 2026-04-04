using Muonroi.Core.Abstractions.Exceptions;
namespace Muonroi.BackgroundJobs.Quartz.Tests;

using QuartzConfigs = Muonroi.BackgroundJobs.Quartz.Quartz.BackgroundJobConfigs;
using QuartzHandler = Muonroi.BackgroundJobs.Quartz.Quartz.BackgroundJobHandler;
using QuartzContextJobListener = Muonroi.BackgroundJobs.Quartz.Quartz.QuartzContextJobListener;
using Muonroi.BackgroundJobs.Abstractions;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public class QuartzConfigsTests
{
    [Fact]
    public void SectionName_Should_Be_BackgroundJobConfigs()
    {
        QuartzConfigs.SectionName.Should().Be("BackgroundJobConfigs");
    }

    [Fact]
    public void Default_JobType_Should_Be_Quartz()
    {
        QuartzConfigs cfg = new();
        cfg.JobType.Should().Be(JobType.Quartz);
    }

    [Fact]
    public void AddBackgroundJobs_With_Wrong_JobType_Should_Throw()
    {
        ServiceCollection services = new();
        Dictionary<string, string?> configDict = new()
        {
            ["BackgroundJobConfigs:JobType"] = "Hangfire"
        };
        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(configDict).Build();

        Action act = () => QuartzHandler.AddBackgroundJobs(services, config);

        act.Should().Throw<MConfigurationException>()
            .WithMessage("*Invalid JobType*Hangfire*Quartz*");
    }

    [Fact]
    public void AddBackgroundJobs_With_Quartz_Should_Register_Services()
    {
        ServiceCollection services = new();
        Dictionary<string, string?> configDict = new()
        {
            ["BackgroundJobConfigs:JobType"] = "Quartz"
        };
        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(configDict).Build();

        QuartzHandler.AddBackgroundJobs(services, config);
        ServiceProvider provider = services.BuildServiceProvider();

        provider.GetService<QuartzContextJobListener>().Should().NotBeNull();
    }

    [Fact]
    public void QuartzContextJobListener_Name_Should_Be_TypeName()
    {
        QuartzContextJobListener listener = new();
        listener.Name.Should().Be(nameof(QuartzContextJobListener));
    }
}
