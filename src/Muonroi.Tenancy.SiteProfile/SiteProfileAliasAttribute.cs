using System;
using Muonroi.Core.Abstractions.Guards;

namespace Muonroi.Tenancy.SiteProfile;

/// <summary>
/// Marks a site profile class as an alias for another site's keyed service registrations.
/// The source generator will emit code that registers all keyed services from the target
/// site as aliases pointing to the target site's implementations.
///
/// When this site needs to diverge: remove this attribute, create dedicated service classes,
/// and register them normally via [GenerateSiteProfile]. Clean migration path (per D-21).
///
/// <example>
/// <code>
/// // SiteB reuses all DEFAULT keyed services — zero boilerplate
/// [SiteProfileAlias("DEFAULT")]
/// [GenerateSiteProfile("SiteB", typeof(DefaultDbContext))]
/// public partial class SiteBProfile : ISiteProfile
/// {
///     public string SiteId => "SiteB";
/// }
/// </code>
/// </example>
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class SiteProfileAliasAttribute : Attribute
{
    /// <summary>
    /// The SiteId of the target site whose keyed services this site aliases.
    /// </summary>
    public string TargetSiteId { get; }

    /// <summary>
    /// Creates a SiteProfileAlias attribute.
    /// </summary>
    /// <param name="targetSiteId">The SiteId to alias (e.g., "DEFAULT").</param>
    public SiteProfileAliasAttribute(string targetSiteId)
    {
        TargetSiteId = MGuard.NotNull(targetSiteId);
    }
}
