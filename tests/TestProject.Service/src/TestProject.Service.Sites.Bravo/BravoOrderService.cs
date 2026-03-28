using TestProject.Service.Core.Contracts;
using TestProject.Service.Core.Services;

namespace TestProject.Service.Sites.Bravo;

/// <summary>
/// Bravo site order service — 70% override: overrides CreateAsync + GetListAsync.
/// </summary>
public sealed class BravoOrderService : OrderServiceBase
{
    /// <inheritdoc />
    public override Task<CreateOrderResult> CreateAsync(string name, string description, CancellationToken ct = default)
    {
        return Task.FromResult(new CreateOrderResult
        {
            Success = true,
            Message = "BRAVO-Created",
            Details = [new OrderDetailDto { OrderDetailNo = "BRAVO01", ContainerNo = name, Id = Guid.NewGuid().ToString() }]
        });
    }

    /// <inheritdoc />
    public override Task<OrderListResult> GetListAsync(string linerCode, int page, int pageSize, CancellationToken ct = default)
    {
        // Bravo returns 2 items instead of the base 1
        return Task.FromResult(new OrderListResult
        {
            Items =
            [
                new OrderDto(1, "BRAVO-Sample-1", "First Bravo order"),
                new OrderDto(2, "BRAVO-Sample-2", "Second Bravo order")
            ],
            Total = 2
        });
    }
}
