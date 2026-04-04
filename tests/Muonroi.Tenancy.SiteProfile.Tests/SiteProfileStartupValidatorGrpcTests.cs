using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Muonroi.Tenancy.SiteProfile.Tests;

/// <summary>
/// Tests for ValidateSiteGrpcClientRegistrations() — startup validation of gRPC client keyed registrations.
/// Because SiteGrpcServiceRegistry is source-generated in consumer projects, tests build a fake registry
/// type dynamically and inject it into the validation logic via reflection-based discovery.
/// </summary>
public class SiteProfileStartupValidatorGrpcTests
{
    // ─── Fake gRPC client types ────────────────────────────────────────────────

    private class FakeGrpcClient { }
    private class FakeGrpcClientAlt { }

    // ─── Helpers ───────────────────────────────────────────────────────────────

    private class CapturingLogger<T> : ILogger<T>
    {
        public readonly List<(LogLevel Level, string Message)> Logs = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Logs.Add((logLevel, formatter(state, exception)));
        }

        public List<string> Warnings => Logs
            .Where(l => l.Level == LogLevel.Warning)
            .Select(l => l.Message)
            .ToList();
    }

    /// <summary>
    /// Builds a ServiceCollection with an embedded fake SiteGrpcServiceRegistry.
    /// The fake registry is registered as a singleton type accessible via reflection
    /// (simulating the source-generated SiteGrpcServiceRegistry found via AppDomain scan).
    /// </summary>
    private static (SiteProfileStartupValidator Validator, CapturingLogger<SiteProfileStartupValidator> Logger)
        BuildValidatorWithRegistry(
            Action<IServiceCollection> configure,
            SiteGrpcServiceRegistryStub? registry = null)
    {
        var logger = new CapturingLogger<SiteProfileStartupValidator>();
        var tracker = new SiteProfileRegistrationTracker();
        tracker.SetSkipValidation(); // Skip normal keyed service validation — focus on gRPC

        var services = new ServiceCollection();
        configure(services);

        if (registry is not null)
        {
            // Register the stub registry so the validator can discover it via AppDomain
            services.AddSingleton(registry);
        }

        var sp = services.BuildServiceProvider();
        var validator = new SiteProfileStartupValidator(tracker, sp, logger);
        return (validator, logger);
    }

    // ─── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public void ValidateSiteGrpcClientRegistrations_AllRegistered_NoWarnings()
    {
        // Arrange: BRAVO has gRPC service + matching keyed client registered
        var registry = new SiteGrpcServiceRegistryStub(
        [
            new SiteGrpcEntryStub("BRAVO", typeof(FakeGrpcClient))
        ]);

        var (validator, logger) = BuildValidatorWithRegistry(services =>
        {
            services.AddKeyedScoped<FakeGrpcClient>("BRAVO");
        }, registry);

        // Act
        var act = () => validator.StartAsync(default);
        act.Should().NotThrowAsync();

        // Assert: no warnings about missing gRPC clients
        logger.Warnings.Should().NotContain(w => w.Contains("[SITE-GRPC]"),
            "no missing gRPC clients means no warnings");
    }

    [Fact]
    public async Task ValidateSiteGrpcClientRegistrations_MissingClientForSite_LogsWarning()
    {
        // Arrange: BRAVO has gRPC service but NO matching keyed client registered
        var registry = new SiteGrpcServiceRegistryStub(
        [
            new SiteGrpcEntryStub("BRAVO", typeof(FakeGrpcClient))
        ]);

        var (validator, logger) = BuildValidatorWithRegistry(_ => { /* no FakeGrpcClient registered */}, registry);

        // Act
        await validator.StartAsync(default);

        // Assert: warning logged for missing BRAVO gRPC client
        logger.Warnings.Should().Contain(w =>
            w.Contains("[SITE-GRPC]") && w.Contains("BRAVO") && w.Contains(nameof(FakeGrpcClient)),
            "missing gRPC client for BRAVO must produce a [SITE-GRPC] warning");
    }

    [Fact]
    public async Task ValidateSiteGrpcClientRegistrations_NoRegistry_IsNoOp()
    {
        // Arrange: no SiteGrpcServiceRegistry in DI or AppDomain — validator must be no-op (D-14)
        var logger = new CapturingLogger<SiteProfileStartupValidator>();
        var tracker = new SiteProfileRegistrationTracker();
        tracker.SetSkipValidation(); // skip normal validation

        var sp = new ServiceCollection().BuildServiceProvider();
        var validator = new SiteProfileStartupValidator(tracker, sp, logger);

        // Act
        var act = () => validator.StartAsync(default);
        await act.Should().NotThrowAsync("no gRPC registry = no-op");

        // Assert: no [SITE-GRPC] warnings
        logger.Warnings.Should().NotContain(w => w.Contains("[SITE-GRPC]"));
    }

    [Fact]
    public async Task ValidateSiteGrpcClientRegistrations_RegistryPresent_NoClients_WarnsAll()
    {
        // Arrange: registry has 2 sites, neither has gRPC client registrations
        var registry = new SiteGrpcServiceRegistryStub(
        [
            new SiteGrpcEntryStub("ALPHA", typeof(FakeGrpcClient)),
            new SiteGrpcEntryStub("BRAVO", typeof(FakeGrpcClientAlt))
        ]);

        var (validator, logger) = BuildValidatorWithRegistry(_ => { /* no keyed clients */}, registry);

        // Act
        await validator.StartAsync(default);

        // Assert: warning for each missing site
        logger.Warnings.Should().Contain(w => w.Contains("[SITE-GRPC]") && w.Contains("ALPHA"),
            "ALPHA missing gRPC client must produce a warning");
        logger.Warnings.Should().Contain(w => w.Contains("[SITE-GRPC]") && w.Contains("BRAVO"),
            "BRAVO missing gRPC client must produce a warning");
    }
}

// ─── Stubs for fake SiteGrpcServiceRegistry ────────────────────────────────────

/// <summary>
/// Simulates a SiteGrpcServiceEntry record (would be source-generated).
/// </summary>
internal record SiteGrpcEntryStub(string SiteId, Type ClientType);

/// <summary>
/// Simulates the source-generated SiteGrpcServiceRegistry.
/// Registered in the test DI container — discovered by SiteProfileStartupValidator
/// via IServiceProvider lookup rather than AppDomain scan (for test isolation).
/// </summary>
internal class SiteGrpcServiceRegistryStub
{
    private readonly IReadOnlyList<SiteGrpcEntryStub> _entries;

    public SiteGrpcServiceRegistryStub(IReadOnlyList<SiteGrpcEntryStub> entries)
    {
        _entries = entries;
    }

    public IReadOnlyList<SiteGrpcEntryStub> GetAllEntries() => _entries;
}
