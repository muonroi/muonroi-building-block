using MuonroiService.Core.Contracts;
using MuonroiService.Core.Services;

namespace MuonroiService.Sites.Default;

/// <summary>
/// Default site order service — uses base implementation without overrides.
/// </summary>
public sealed class DefaultOrderService : OrderServiceBase
{
    // No overrides — uses base behavior.
    // Override CreateAsync or GetByIdAsync here if the default site needs custom logic.
}
