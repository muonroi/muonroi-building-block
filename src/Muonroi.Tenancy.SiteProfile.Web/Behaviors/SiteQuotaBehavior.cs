using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Muonroi.Core.Abstractions.Exceptions;
using Muonroi.Quota.Abstractions;
using Muonroi.Tenancy.SiteProfile;

namespace Muonroi.Tenancy.SiteProfile.Web.Behaviors;

/// <summary>
/// Thrown when a per-site quota limit is exceeded.
/// </summary>
public sealed class SiteQuotaExceededException : MException
{
    /// <summary>The site that exhausted its quota.</summary>
    public string SiteId { get; }

    /// <summary>The quota type that was exhausted.</summary>
    public QuotaType QuotaType { get; }

    /// <summary>The amount of quota requested that caused the violation.</summary>
    public int RequestedAmount { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="SiteQuotaExceededException"/>.
    /// </summary>
    public SiteQuotaExceededException(string siteId, QuotaType quotaType, int requestedAmount)
        : base("SITE_QUOTA_EXCEEDED", $"Quota '{quotaType}' exceeded for site '{siteId}' (requested: {requestedAmount}).", MExceptionCategory.Domain, 429)
    {
        SiteId = siteId;
        QuotaType = quotaType;
        RequestedAmount = requestedAmount;
        Details["SiteId"] = siteId;
        Details["QuotaType"] = quotaType;
        Details["RequestedAmount"] = requestedAmount;
    }
}
/// <summary>
/// Per-site quota enforcer. Checks quota via ITenantQuotaTracker and increments on success;
/// throws <see cref="SiteQuotaExceededException"/> when the limit is reached.
/// </summary>
public interface ISiteQuotaEnforcer
{
    /// <summary>
    /// Enforces a quota check for this site. Throws <see cref="SiteQuotaExceededException"/>
    /// when the requested amount would exceed the configured limit.
    /// </summary>
    Task EnforceAsync(QuotaType type, int amount = 1, CancellationToken ct = default);
}

/// <summary>
/// Built-in ISiteProfileBehavior that enforces per-site quota limits using ITenantQuotaTracker.
/// Decorate your ISiteProfile with [SiteProfileBehavior(typeof(SiteQuotaBehavior))].
///
/// Requires: ITenantQuotaTracker registered in DI (use AddInMemoryQuotaTracking() or a custom impl).
///
/// Usage:
/// <code>
/// var enforcer = sp.GetRequiredKeyedService&lt;ISiteQuotaEnforcer&gt;("TCI");
/// await enforcer.EnforceAsync(QuotaType.ApiRequestsPerMinute);
/// </code>
/// </summary>
public sealed class SiteQuotaBehavior : ISiteProfileBehavior
{
    /// <inheritdoc />
    public void Apply(IServiceCollection services, IConfiguration configuration, string siteId)
    {
        services.AddKeyedScoped<ISiteQuotaEnforcer>(siteId, (sp, _) =>
        {
            var tracker = sp.GetRequiredService<ITenantQuotaTracker>();
            return new SiteQuotaEnforcer(siteId, tracker);
        });
    }
}

/// <summary>
/// Default ISiteQuotaEnforcer implementation — delegates to ITenantQuotaTracker.
/// </summary>
internal sealed class SiteQuotaEnforcer : ISiteQuotaEnforcer
{
    private readonly string _siteId;
    private readonly ITenantQuotaTracker _tracker;

    public SiteQuotaEnforcer(string siteId, ITenantQuotaTracker tracker)
    {
        _siteId = siteId;
        _tracker = tracker;
    }

    public async Task EnforceAsync(QuotaType type, int amount = 1, CancellationToken ct = default)
    {
        bool allowed = await _tracker.CheckQuotaAsync(_siteId, type, amount, ct).ConfigureAwait(false);
        if (!allowed)
            throw new SiteQuotaExceededException(_siteId, type, amount);

        await _tracker.IncrementUsageAsync(_siteId, type, amount, ct).ConfigureAwait(false);
    }
}
