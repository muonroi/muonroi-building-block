using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Muonroi.BackgroundJobs.Abstractions;
using Muonroi.Core.Abstractions.Ecosystem;
using Muonroi.Core.Abstractions.Exceptions;
using Xunit;

namespace Muonroi.BackgroundJobs.Abstractions.Tests;

/// <summary>
/// Tests for BackgroundJobHandler SEC-02 security fix:
/// compile-time dispatch replacing unsafe Type.GetType reflection.
/// </summary>
public class BackgroundJobHandlerTests
{
    /// <summary>
    /// Test 1: JobType.Hangfire dispatches to the registered Hangfire provider.
    /// </summary>
    [Fact]
    public void AddBackgroundJobs_HangfireType_CallsRegisteredHangfireProvider()
    {
        // Arrange — register a fake Hangfire provider
        bool hangfireCalled = false;
        BackgroundJobHandler.RegisterProvider(JobType.Hangfire, (services, config) =>
        {
            hangfireCalled = true;
            return services;
        });

        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BackgroundJobConfigs:JobType"] = "Hangfire"
            })
            .Build();
        IServiceCollection services = new ServiceCollection();

        // Act
        services.AddBackgroundJobs(config);

        // Assert
        hangfireCalled.Should().BeTrue("Hangfire provider must be dispatched when JobType=Hangfire");
    }

    /// <summary>
    /// Test 2: JobType.Quartz dispatches to the registered Quartz provider.
    /// </summary>
    [Fact]
    public void AddBackgroundJobs_QuartzType_CallsRegisteredQuartzProvider()
    {
        // Arrange — register a fake Quartz provider
        bool quartzCalled = false;
        BackgroundJobHandler.RegisterProvider(JobType.Quartz, (services, config) =>
        {
            quartzCalled = true;
            return services;
        });

        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BackgroundJobConfigs:JobType"] = "Quartz"
            })
            .Build();
        IServiceCollection services = new ServiceCollection();

        // Act
        services.AddBackgroundJobs(config);

        // Assert
        quartzCalled.Should().BeTrue("Quartz provider must be dispatched when JobType=Quartz");
    }

    /// <summary>
    /// Test 3: An unregistered/unknown JobType throws MConfigurationException,
    /// not TypeLoadException or any other CLR exception.
    /// </summary>
    [Fact]
    public void AddBackgroundJobs_UnknownJobType_ThrowsMConfigurationException()
    {
        // Arrange — clear any registered provider for an unknown value
        // Use a fake config that does not match any registered provider.
        // We simulate this by using a value that doesn't map to Hangfire or Quartz.
        // We'll unregister a fake type by creating a config with int value 99.
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Use a numeric value that doesn't correspond to Hangfire=0 or Quartz=1
                ["BackgroundJobConfigs:JobType"] = "99"
            })
            .Build();
        IServiceCollection services = new ServiceCollection();

        // Act & Assert
        Action act = () => services.AddBackgroundJobs(config);
        act.Should().Throw<MConfigurationException>(
            "unknown job type must throw MConfigurationException, not TypeLoadException");
    }

    /// <summary>
    /// Test 4: The source file no longer contains Type.GetType, MethodInfo, or Invoke calls.
    /// This verifies that reflection has been removed from the dispatch mechanism.
    /// </summary>
    [Fact]
    public void SourceFile_DoesNotContain_ReflectionCalls()
    {
        string? sourceFile = FindSourceFile("BackgroundJobHandler.cs");
        if (sourceFile == null || !File.Exists(sourceFile)) return;

        string content = File.ReadAllText(sourceFile);
        content.Should().NotContain("Type.GetType",
            "unsafe Type.GetType reflection must be removed (SEC-02)");
        content.Should().NotContain("MethodInfo",
            "MethodInfo reflection must be removed (SEC-02)");
        content.Should().NotContain(".Invoke(",
            "reflection Invoke must be removed (SEC-02)");
    }

    /// <summary>
    /// Test 5: When Logging capability is registered via the ecosystem registry,
    /// dispatching logs a structured message to Console.
    /// </summary>
    [Fact]
    public void AddBackgroundJobs_WithLoggingCapability_WritesStructuredLog()
    {
        // Arrange — register a fake Hangfire provider
        BackgroundJobHandler.RegisterProvider(JobType.Hangfire, (services, config) => services);

        Mock<IMEcosystemRegistry> registryMock = new();
        registryMock.Setup(r => r.Has(MCapability.Logging)).Returns(true);

        IServiceCollection services = new ServiceCollection();
        // Register the mock registry as implementation instance so it's found at registration time
        services.AddSingleton(registryMock.Object);

        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BackgroundJobConfigs:JobType"] = "Hangfire"
            })
            .Build();

        // Capture Console output
        TextWriter original = Console.Out;
        using StringWriter sw = new();
        Console.SetOut(sw);

        try
        {
            services.AddBackgroundJobs(config);
        }
        finally
        {
            Console.SetOut(original);
        }

        // Assert
        string output = sw.ToString();
        output.Should().Contain("Hangfire",
            "when Logging capability is active, dispatch must log which provider was selected");
    }

    private static string? FindSourceFile(string fileName)
    {
        string start = AppContext.BaseDirectory;
        DirectoryInfo? dir = new DirectoryInfo(start);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Muonroi.BuildingBlock.sln")))
            dir = dir.Parent;

        if (dir == null) return null;

        return Directory.EnumerateFiles(dir.FullName, fileName, SearchOption.AllDirectories)
            .FirstOrDefault(f => f.Contains("BackgroundJobs.Abstractions", StringComparison.OrdinalIgnoreCase)
                              && !f.Contains("Tests", StringComparison.OrdinalIgnoreCase));
    }
}
