namespace Muonroi.Tenancy.SiteProfile.Tests;

public class SiteProfileStartupValidatorTests
{
    private interface IMyService { }
    private class MyServiceImpl : IMyService { }

    private class TestLogger<T> : ILogger<T>
    {
        public List<string> Warnings = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning) Warnings.Add(formatter(state, exception));
        }
    }

    private readonly TestLogger<SiteProfileStartupValidator> _logger = new();

    [Fact]
    public void SkipValidation_DoesNotThrow()
    {
        var tracker = new SiteProfileRegistrationTracker();
        tracker.SetSkipValidation();
        tracker.RecordSiteId("TCI");
        tracker.RecordResolvedServiceType(typeof(IMyService));

        var sp = new ServiceCollection().BuildServiceProvider();
        var validator = new SiteProfileStartupValidator(tracker, sp, _logger);

        var act = () => validator.StartAsync(default);
        act.Should().NotThrowAsync();
    }

    [Fact]
    public void AllKeyedServicesPresent_Passes()
    {
        var tracker = new SiteProfileRegistrationTracker();
        tracker.RecordSiteId("TCI");
        tracker.RecordResolvedServiceType(typeof(IMyService));

        var services = new ServiceCollection();
        services.AddKeyedScoped<IMyService, MyServiceImpl>("TCI");
        var sp = services.BuildServiceProvider();

        var validator = new SiteProfileStartupValidator(tracker, sp, _logger);

        var act = () => validator.StartAsync(default);
        act.Should().NotThrowAsync();
    }

    [Fact]
    public void MissingKeyedService_ThrowsInvalidOperationException()
    {
        var tracker = new SiteProfileRegistrationTracker();
        tracker.RecordSiteId("TCI");
        tracker.RecordResolvedServiceType(typeof(IMyService));

        var sp = new ServiceCollection().BuildServiceProvider(); // No keyed registration
        var validator = new SiteProfileStartupValidator(tracker, sp, _logger);

        var act = () => validator.StartAsync(default);
        act.Should().ThrowAsync<MInternalException>()
            .WithMessage("*Site 'TCI' x Service 'IMyService': no keyed registration for key \"TCI\" or \"default\"*");
    }

    [Fact]
    public void MissingKeyedService_WithDefaultFallback_WarnsButPasses()
    {
        var tracker = new SiteProfileRegistrationTracker();
        tracker.RecordSiteId("TCI");
        tracker.RecordResolvedServiceType(typeof(IMyService));

        var services = new ServiceCollection();
        services.AddKeyedScoped<IMyService, MyServiceImpl>("default");
        var sp = services.BuildServiceProvider();

        var validator = new SiteProfileStartupValidator(tracker, sp, _logger);

        var act = () => validator.StartAsync(default);
        act.Should().NotThrowAsync();

        // Should have logged a warning
        _logger.Warnings.Should().NotBeEmpty();
        _logger.Warnings[0].Should().Contain("Site 'TCI' has no keyed registration for 'IMyService'");
    }

    [Fact]
    public void MissingKeyedService_WithDefaultFallback_StrictMode_Throws()
    {
        var tracker = new SiteProfileRegistrationTracker();
        tracker.RecordSiteId("TCI");
        tracker.RecordResolvedServiceType(typeof(IMyService));

        var services = new ServiceCollection();
        services.AddKeyedScoped<IMyService, MyServiceImpl>("default");
        services.Configure<SiteProfileOptions>(o => o.StrictMode = true);
        var sp = services.BuildServiceProvider();

        var validator = new SiteProfileStartupValidator(tracker, sp, _logger);

        var act = () => validator.StartAsync(default);
        act.Should().ThrowAsync<MInternalException>()
            .WithMessage("*no site-specific registration (StrictMode rejects 'default' fallback)*");
    }

    [Fact]
    public void MultipleGaps_MessageListsAll()
    {
        var tracker = new SiteProfileRegistrationTracker();
        tracker.RecordSiteId("TCI");
        tracker.RecordSiteId("HNI");
        tracker.RecordResolvedServiceType(typeof(IMyService));

        var sp = new ServiceCollection().BuildServiceProvider();
        var validator = new SiteProfileStartupValidator(tracker, sp, _logger);

        var act = () => validator.StartAsync(default);
        act.Should().ThrowAsync<MInternalException>()
            .WithMessage("*Site 'TCI' x Service 'IMyService'*")
            .WithMessage("*Site 'HNI' x Service 'IMyService'*");
    }
}
