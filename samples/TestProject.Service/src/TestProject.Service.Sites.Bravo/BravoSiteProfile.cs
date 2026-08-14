namespace TestProject.Service.Sites.Bravo;

/// <summary>
/// Bravo site profile — 70% override (overrides CreateAsync + GetListAsync + has index).
/// Source generator auto-creates RegisterServices().
/// </summary>
[GenerateSiteProfile(SiteIds.BRAVO, typeof(BravoOrderContext))]
public partial class BravoSiteProfile : ISiteProfile
{
    public string SiteId => SiteIds.BRAVO;
}
