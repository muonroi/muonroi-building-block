namespace TestProject.Aggregate.Sites.Default;

/// <summary>
/// Default site profile — handler-based aggregate dispatch, no EF Core DbContext.
/// Source generator auto-creates RegisterServices() from [GenerateSiteProfile] attribute.
/// </summary>
[GenerateSiteProfile(SiteIds.DEFAULT, typeof(object), SkipDbContextRegistration = true)]
public partial class DefaultSiteProfile : ISiteProfile
{
    public string SiteId => SiteIds.DEFAULT;
}
