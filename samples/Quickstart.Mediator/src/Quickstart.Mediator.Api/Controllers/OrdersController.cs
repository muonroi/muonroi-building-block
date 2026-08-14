namespace Quickstart.Mediator.Api.Controllers;

/// <summary>
/// REST API surface for the orders quickstart.
/// All actions delegate to the mediator — the controller has zero business logic.
/// </summary>
[ApiController]
[Route("api/orders")]
public sealed class OrdersController(IMediator mediator) : ControllerBase
{
    // POST api/orders
    // Dispatches CreateOrderCommand → CreateOrderCommandHandler (one handler).
    // The pre-processor, timing behavior, validation, post-processor all run automatically.
    [HttpPost]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateOrder(
        [FromBody] CreateOrderCommand command,
        CancellationToken cancellationToken)
    {
        OrderDto order = await mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetOrder), new { id = order.Id }, order);
    }

    // GET api/orders/{id}
    // Dispatches GetOrderQuery → GetOrderQueryHandler (one handler).
    [HttpGet("{id:guid}", Name = nameof(GetOrder))]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOrder(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            OrderDto order = await mediator.Send(new GetOrderQuery(id), cancellationToken);
            return Ok(order);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    // GET api/orders/stream?count=10
    // Dispatches ListOrdersStreamQuery → ListOrdersStreamHandler via mediator.CreateStream().
    // Returns IAsyncEnumerable<OrderDto> — ASP.NET Core serializes it as a JSON array streamed
    // to the client without buffering the entire result set.
    [HttpGet("stream")]
    [ProducesResponseType(typeof(IAsyncEnumerable<OrderDto>), StatusCodes.Status200OK)]
    public IAsyncEnumerable<OrderDto> StreamOrders(
        [FromQuery] int count = 100,
        CancellationToken cancellationToken = default)
    {
        return mediator.CreateStream(new ListOrdersStreamQuery(count), cancellationToken);
    }

    // DELETE api/orders/{id}
    // Requires "orders:delete" permission (enforced by MAuthorizationBehavior via [MAuthorize]).
    // Pass the permission in the X-Permissions header; the middleware in Program.cs
    // populates ISystemExecutionContext.Permissions from that header.
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteOrder(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await mediator.Send(new DeleteOrderCommand(id), cancellationToken);
            return NoContent();
        }
        catch (MForbiddenException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Access denied.",
                requiredRoles = ex.RequiredRoles,
                requiredPermissions = ex.RequiredPermissions
            });
        }
    }
}
