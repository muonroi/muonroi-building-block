#if (isAggregate)
using Microsoft.AspNetCore.Mvc;
using MuonroiService.Core.Commands;
using MuonroiService.Core.Contracts;
using MuonroiService.Core.Validators;
using Muonroi.Tenancy.SiteProfile;

namespace MuonroiService.Host.Controllers;

/// <summary>
/// REST controller for order operations.
/// Aggregate type — routes through site-resolved IOrderService.
/// Each site's handler can be overridden via Sites/{SiteName}/Handlers/.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
public class OrderController : ControllerBase
{
    private readonly IOrderService _orderService;

    public OrderController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOrderCommand command, CancellationToken ct)
    {
        var errors = CreateOrderCommandValidator.Validate(command).ToList();
        if (errors.Count > 0)
            return BadRequest(new { errors });

        var result = await _orderService.CreateAsync(command.Name, command.Description, ct);
        return Ok(result);
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken ct)
    {
        var result = await _orderService.GetByIdAsync(id, ct);
        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] string linerCode, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _orderService.GetListAsync(linerCode, page, pageSize, ct);
        return Ok(result);
    }
}
#endif
