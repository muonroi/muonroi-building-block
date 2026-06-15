using Microsoft.AspNetCore.Mvc;
using Muonroi.Mapper.Mapper;
using Quickstart.Mapper.Api.Models;

namespace Quickstart.Mapper.Api.Controllers;

/// <summary>
/// Demonstrates the Muonroi mapper.
/// Injects IMapper (resolved to SimpleMapper) and maps a Product entity to a ProductDto.
/// </summary>
[ApiController]
[Route("api/mapping")]
public sealed class MappingController(IMapper mapper) : ControllerBase
{
    // POST api/mapping/to-dto
    // Maps the posted Product entity to a new ProductDto via Map<TDestination>(source).
    // See src/Muonroi.Mapper/Mapper/SimpleMapper.cs:11.
    [HttpPost("to-dto")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    public IActionResult ToDto([FromBody] Product product)
    {
        ProductDto dto = mapper.Map<ProductDto>(product);
        return Ok(dto);
    }

    // POST api/mapping/onto-existing
    // Maps a sample Product onto an existing ProductDto instance via
    // Map<TSource,TDestination>(source, destination).
    // See src/Muonroi.Mapper/Mapper/SimpleMapper.cs:22.
    [HttpPost("onto-existing")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    public IActionResult OntoExisting([FromBody] Product product)
    {
        ProductDto existing = new();
        ProductDto result = mapper.Map<Product, ProductDto>(product, existing);
        return Ok(result);
    }
}
