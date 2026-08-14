namespace TestProject.Service.Sites.Default;

/// <summary>
/// Default fallback site profile — most sites use this. Zero overrides.
/// Source generator auto-creates RegisterServices().
/// </summary>
[GenerateSiteProfile(SiteIds.DEFAULT, typeof(DefaultOrderContext))]
public partial class DefaultSiteProfile : ISiteProfile
{
    public string SiteId => SiteIds.DEFAULT;
}
