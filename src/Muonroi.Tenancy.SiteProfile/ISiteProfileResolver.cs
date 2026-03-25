namespace Muonroi.Tenancy.SiteProfile;

/// <summary>
/// Resolves the current site profile per-request.
/// Scoped lifetime — one instance per HTTP request.
/// Inject to determine which site the current request belongs to.
/// </summary>
/// <remarks>
/// Registered automatically by <see cref="SiteProfileExtensions.AddMultiSiteProfiles"/>.
/// Resolution chain: siteCodeAccessor → dictionary lookup → "default" fallback → exception.
/// </remarks>
public interface ISiteProfileResolver
{
    /// <summary>Gets the site profile for the current request's site code.</summary>
    ISiteProfile Current { get; }
}

/// <summary>
/// Default implementation — holds a resolved ISiteProfile instance.
/// </summary>
public sealed class SiteProfileResolver(ISiteProfile profile) : ISiteProfileResolver
{
    /// <inheritdoc />
    public ISiteProfile Current => profile;
}
