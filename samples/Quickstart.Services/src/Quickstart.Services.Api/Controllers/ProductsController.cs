namespace Quickstart.Services.Api.Controllers;

/// <summary>
/// REST surface over ProductService (a MServiceBase&lt;Product, ProductDto&gt; subclass).
/// All persistence logic lives in the generic base; this controller only adapts HTTP.
/// </summary>
[ApiController]
[Route("api/products")]
public sealed class ProductsController(ProductService service, AppDbContext db) : ControllerBase
{
    // POST api/products
    // MServiceBase.CreateAsync runs ValidateAsync -> ApplyDefaultValues -> BeforeCreate -> save -> AfterCreate.
    // See src/Muonroi.Services/MServiceBase.cs:64.
    [HttpPost]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] ProductDto dto, CancellationToken ct)
    {
        ProductDto created = await service.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    // GET api/products/{id}
    // MServiceBase.GetByIdAsync finds via Set<Product>().FindAsync and maps to DTO.
    // See src/Muonroi.Services/MServiceBase.cs:42.
    [HttpGet("{id:long}", Name = nameof(GetById))]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(long id, CancellationToken ct)
    {
        ProductDto? dto = await service.GetByIdAsync(id, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    // PUT api/products/{id}
    // Loads the tracked entity, then MServiceBase.UpdateAsync applies the DTO and saves.
    // See src/Muonroi.Services/MServiceBase.cs:79.
    [HttpPut("{id:long}")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(long id, [FromBody] ProductDto dto, CancellationToken ct)
    {
        Product? entity = await db.Products.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (entity is null)
        {
            return NotFound();
        }

        ProductDto updated = await service.UpdateAsync(entity, dto, ct);
        return Ok(updated);
    }

    // DELETE api/products/{id}
    // See src/Muonroi.Services/MServiceBase.cs:93.
    [HttpDelete("{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        Product? entity = await db.Products.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (entity is null)
        {
            return NotFound();
        }

        bool deleted = await service.DeleteAsync(entity, ct);
        return deleted ? NoContent() : NotFound();
    }
}
