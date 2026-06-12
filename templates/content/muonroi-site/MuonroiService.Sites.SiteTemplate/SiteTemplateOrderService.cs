using MuonroiService.Core.Services;

namespace MuonroiService.Sites.SiteTemplate;

/// <summary>
/// SITE_ID_UPPER site order service — override methods for site-specific business logic.
/// </summary>
public sealed class SiteTemplateOrderService : OrderServiceBase
{
    // Override CreateAsync, GetByIdAsync, GetListAsync for SITE_ID_UPPER-specific logic.
}
