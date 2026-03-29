using TestProject.Service.Core.Contracts;

namespace TestProject.Service.Core.Services;

/// <summary>
/// Base implementation of <see cref="IOrderService"/> with virtual methods.
/// Site-specific projects inherit this and override only the methods that differ.
/// </summary>
public abstract class OrderServiceBase : IOrderService
{
    /// <inheritdoc />
    public virtual Task<CreateOrderResult> CreateAsync(string name, string description, CancellationToken ct = default)
    {
        return Task.FromResult(new CreateOrderResult
        {
            Success = true,
            Message = "Created",
            Details = [new OrderDetailDto { OrderDetailNo = "SAMPLE01", ContainerNo = name, Id = Guid.NewGuid().ToString() }]
        });
    }

    /// <inheritdoc />
    public virtual Task<OrderDto> GetByIdAsync(long id, CancellationToken ct = default)
    {
        return Task.FromResult(new OrderDto(id, "Sample", "Sample order"));
    }

    /// <inheritdoc />
    public virtual Task<OrderListResult> GetListAsync(string linerCode, int page, int pageSize, CancellationToken ct = default)
    {
        return Task.FromResult(new OrderListResult
        {
            Items = [new OrderDto(1, "Sample", "Sample order")],
            Total = 1
        });
    }
}
