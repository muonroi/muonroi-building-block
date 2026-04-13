using Muonroi.Tenancy.SiteProfile;
using TestProject.Aggregate.Core.Constants;

namespace TestProject.Aggregate.Sites.Bravo;

/// <summary>
/// Bravo site profile — handler-based aggregate dispatch, no EF Core DbContext.
/// Bravo also has a per-site .proto with unique RPCs.
/// Source generator auto-creates RegisterServices() from [GenerateSiteProfile] attribute.
/// </summary>
[GenerateSiteProfile(SiteIds.BRAVO, typeof(object), SkipDbContextRegistration = true)]
public partial class BravoSiteProfile : ISiteProfile
{
    public string SiteId => SiteIds.BRAVO;
}
