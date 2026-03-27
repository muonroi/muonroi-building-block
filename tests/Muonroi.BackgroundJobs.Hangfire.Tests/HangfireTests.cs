namespace Muonroi.BackgroundJobs.Hangfire.Tests;

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Muonroi.BackgroundJobs.Hangfire.Hangfire;
using Muonroi.BackgroundJobs.Abstractions;
using Xunit;

public class HangfireTests
{
    [Fact]
    public void AddBackgroundJobs_ShouldRegisterHangfireServices()
    {
        // Arrange
        var services = new ServiceCollection();
        var configDict = new Dictionary<string, string?>
        {
            ["BackgroundJobConfigs:JobType"] = "Hangfire"
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(configDict).Build();
        
        // Act
        // Initialize static JobStorage for Hangfire
        global::Hangfire.JobStorage.Current = new global::Hangfire.MemoryStorage.MemoryStorage();
        
        Muonroi.BackgroundJobs.Hangfire.Hangfire.BackgroundJobHandler.AddBackgroundJobs(services, config);
        var provider = services.BuildServiceProvider();

        // Assert
        Assert.NotNull(provider.GetService<JobContextActivatorFilter>());
        Assert.NotNull(provider.GetService<global::Hangfire.IGlobalConfiguration>());
    }
}
