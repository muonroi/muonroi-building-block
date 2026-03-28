using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Muonroi.Logging.Abstractions;
using Muonroi.Tenancy.SiteProfile;

namespace Muonroi.Tenancy.SiteProfile.Web.Behaviors;

/// <summary>
/// Built-in ISiteProfileBehavior that logs site registration and resolution events via IMLog.
/// Decorate your ISiteProfile with [SiteProfileBehavior(typeof(SiteAuditBehavior))].
///
/// Emits:
///   "SiteId: {SiteId} registered" — on DI registration (via IHostedService startup)
///   "SiteId: {SiteId} resolved"   — on ISiteProfileResolver.Current access (via ISiteProfileAuditScope, deferred to Plan 02)
/// </summary>
public sealed class SiteAuditBehavior : ISiteProfileBehavior
{
    /// <inheritdoc />
    public void Apply(IServiceCollection services, IConfiguration configuration, string siteId)
    {
        // Register a hosted startup logger that emits the registration event once at app startup.
        // IMLog<SiteAuditBehavior> is resolved from the built container inside the factory lambda.
        services.AddSingleton<IHostedService>(sp =>
        {
            var log = sp.GetRequiredService<IMLog<SiteAuditBehavior>>();
            return new SiteAuditStartupLogger(siteId, log);
        });
    }
}

/// <summary>
/// IHostedService that logs the site registration event once at application startup.
/// </summary>
internal sealed class SiteAuditStartupLogger : IHostedService
{
    private readonly string _siteId;
    private readonly IMLog<SiteAuditBehavior> _log;

    public SiteAuditStartupLogger(string siteId, IMLog<SiteAuditBehavior> log)
    {
        _siteId = siteId;
        _log = log;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _log.Info("SiteId: {SiteId} registered", _siteId);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
