namespace Quickstart.OpenApi.Api.Controllers;

/// <summary>
/// Controller whose endpoints showcase the Muonroi OpenApi filters:
///   - The default-valued <c>pageSize</c> parameter is surfaced by SwaggerDefaultValues.
///   - MErrorResponseFilter auto-adds 400/500 MErrorResponse docs to every action.
/// </summary>
[ApiController]
[Route("api/catalog")]
[Produces("application/json")]
public sealed class CatalogController : ControllerBase
{
    private static readonly CatalogItem[] Items =
    [
        new(1, "Widget", 9.99m),
        new(2, "Gadget", 19.99m),
        new(3, "Gizmo", 29.99m)
    ];

    // GET api/catalog?page=1&pageSize=10
    // pageSize has a compile-time default (10) — SwaggerDefaultValues reads it via
    // the API explorer and writes it into the generated OpenAPI schema.
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<CatalogItem>), StatusCodes.Status200OK)]
    public IActionResult List([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        IEnumerable<CatalogItem> result = Items
            .Skip((Math.Max(1, page) - 1) * pageSize)
            .Take(pageSize);
        return Ok(result);
    }

    // GET api/catalog/{id}
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(CatalogItem), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Get(int id)
    {
        CatalogItem? item = Items.FirstOrDefault(i => i.Id == id);
        return item is null ? NotFound() : Ok(item);
    }
}
