using Muonroi.Tenancy.SiteProfile;
using MuonroiService.Core.Constants;
#if (isEf)
using MuonroiService.Core.Persistence;
#endif

namespace MuonroiService.Sites.Default;

/// <summary>
/// Default fallback site profile — most sites use this. Zero overrides.
/// Source generator auto-creates RegisterServices().
/// </summary>
#if (isEf)
[GenerateSiteProfile(SiteIds.DEFAULT, typeof(DefaultOrderContext))]
#else
[GenerateSiteProfile(SiteIds.DEFAULT, typeof(object), SkipDbContextRegistration = true)]
#endif
public partial class DefaultSiteProfile : ISiteProfile
{
    public string SiteId => SiteIds.DEFAULT;
}
