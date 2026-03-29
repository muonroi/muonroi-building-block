namespace TestProject.Service.Core.Contracts;

/// <summary>
/// Service interface for order operations. Each site provides its own implementation
/// registered as a keyed service via <c>[GenerateSiteProfile]</c>.
/// </summary>
public interface IOrderService
{
    /// <summary>Creates a new order.</summary>
    Task<CreateOrderResult> CreateAsync(string name, string description, CancellationToken ct = default);

    /// <summary>Gets an order by ID.</summary>
    Task<OrderDto> GetByIdAsync(long id, CancellationToken ct = default);

    /// <summary>Gets a list of orders.</summary>
    Task<OrderListResult> GetListAsync(string linerCode, int page, int pageSize, CancellationToken ct = default);
}

/// <summary>Result from CreateAsync.</summary>
public class CreateOrderResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public List<OrderDetailDto> Details { get; set; } = [];
}

/// <summary>Single order detail result.</summary>
public class OrderDetailDto
{
    public string? OrderDetailNo { get; set; }
    public string? ContainerNo { get; set; }
    public string? Id { get; set; }
}

/// <summary>DTO returned by GetByIdAsync.</summary>
public record OrderDto(long Id, string? Name, string? Description);

/// <summary>Result from GetListAsync.</summary>
public class OrderListResult
{
    public List<OrderDto> Items { get; set; } = [];
    public int Total { get; set; }
}
