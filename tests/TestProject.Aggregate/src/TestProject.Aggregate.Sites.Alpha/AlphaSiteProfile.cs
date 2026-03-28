using Muonroi.Tenancy.SiteProfile;
using TestProject.Aggregate.Core.Constants;

namespace TestProject.Aggregate.Sites.Alpha;

/// <summary>
/// Alpha site profile — handler-based aggregate dispatch, no EF Core DbContext.
/// Source generator auto-creates RegisterServices() from [GenerateSiteProfile] attribute.
/// </summary>
[GenerateSiteProfile(SiteIds.ALPHA, typeof(object), SkipDbContextRegistration = true)]
public partial class AlphaSiteProfile : ISiteProfile
{
    public string SiteId => SiteIds.ALPHA;
}
