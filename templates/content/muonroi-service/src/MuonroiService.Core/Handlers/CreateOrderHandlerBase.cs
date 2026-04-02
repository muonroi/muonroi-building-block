#if (isAggregate)
using MuonroiService.Core.Commands;
using MuonroiService.Core.Contracts;

namespace MuonroiService.Core.Handlers;

/// <summary>
/// Base handler for <see cref="CreateOrderCommand"/>.
/// Aggregate type: delegates to downstream service via site-resolved IOrderService.
/// Override in Sites/{SiteName}/Handlers/ for site-specific routing.
/// </summary>
public abstract class CreateOrderHandlerBase
{
    private readonly IServiceProvider _serviceProvider;

    protected CreateOrderHandlerBase(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public virtual async Task<CreateOrderResult> HandleAsync(CreateOrderCommand command)
    {
        IOrderService service = ResolveService(command);
        return await service.CreateAsync(command.Name, command.Description, command.CancellationToken);
    }

    protected virtual IOrderService ResolveService(CreateOrderCommand command)
    {
        // Default: use the base IOrderService (site-resolved by site profile)
        return _serviceProvider.GetRequiredService<IOrderService>();
    }
}
#endif
