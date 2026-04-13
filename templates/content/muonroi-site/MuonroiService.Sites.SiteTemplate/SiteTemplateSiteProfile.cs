using Muonroi.Tenancy.SiteProfile;

namespace MuonroiService.Sites.SiteTemplate;

/// <summary>
/// SiteTemplate site profile — source generator auto-creates RegisterServices().
/// </summary>
#if (isEf)
[GenerateSiteProfile("SITE_ID_UPPER", typeof(SiteTemplateOrderContext))]
#else
[GenerateSiteProfile("SITE_ID_UPPER", typeof(object), SkipDbContextRegistration = true)]
#endif
public partial class SiteTemplateSiteProfile : ISiteProfile
{
    public string SiteId => "SITE_ID_UPPER";
}
