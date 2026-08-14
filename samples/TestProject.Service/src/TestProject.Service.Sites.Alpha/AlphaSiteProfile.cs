namespace TestProject.Service.Sites.Alpha;

/// <summary>
/// Alpha site profile — 30% override (overrides CreateAsync).
/// Source generator auto-creates RegisterServices().
/// </summary>
[GenerateSiteProfile(SiteIds.ALPHA, typeof(AlphaOrderContext))]
public partial class AlphaSiteProfile : ISiteProfile
{
    public string SiteId => SiteIds.ALPHA;
}
