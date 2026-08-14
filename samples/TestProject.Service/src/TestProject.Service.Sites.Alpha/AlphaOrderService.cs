namespace TestProject.Service.Sites.Alpha;

/// <summary>
/// Alpha site order service — 30% override: overrides CreateAsync with ALPHA prefix.
/// </summary>
public sealed class AlphaOrderService : OrderServiceBase
{
    /// <inheritdoc />
    public override Task<CreateOrderResult> CreateAsync(string name, string description, CancellationToken ct = default)
    {
        return Task.FromResult(new CreateOrderResult
        {
            Success = true,
            Message = "ALPHA-Created",
            Details = [new OrderDetailDto { OrderDetailNo = "ALPHA01", ContainerNo = name, Id = Guid.NewGuid().ToString() }]
        });
    }
}
