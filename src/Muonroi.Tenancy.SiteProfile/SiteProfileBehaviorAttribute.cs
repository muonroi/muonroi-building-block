namespace Muonroi.Tenancy.SiteProfile;

/// <summary>
/// Decorates an ISiteProfile class to apply a cross-site behavior during registration.
/// Multiple behaviors per site are supported via repeated attributes.
/// <code>
/// [SiteProfileBehavior(typeof(AuditLoggingBehavior))]
/// [SiteProfileBehavior(typeof(HealthCheckBehavior))]
/// public class TciSiteProfile : ISiteProfile { ... }
/// </code>
/// </summary>
/// <remarks>
/// Creates a SiteProfileBehavior attribute.
/// </remarks>
/// <param name="behaviorType">Type implementing ISiteProfileBehavior.</param>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class SiteProfileBehaviorAttribute(Type behaviorType) : Attribute
{
    /// <summary>The ISiteProfileBehavior implementation type to apply.</summary>
    public Type BehaviorType { get; } = behaviorType;
}
