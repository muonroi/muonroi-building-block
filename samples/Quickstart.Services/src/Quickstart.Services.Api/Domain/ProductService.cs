using Muonroi.Mapping.Abstractions;
using Muonroi.Services;

namespace Quickstart.Services.Api.Domain;

/// <summary>
/// Concrete service over Product/ProductDto. Inherits all CRUD from MServiceBase
/// and overrides one hook to show the extension-point pattern.
/// See src/Muonroi.Services/MServiceBase.cs:25 (base) and :118 (ApplyDefaultValues hook).
/// </summary>
public sealed class ProductService(AppDbContext context, IEntityMapper<Product, ProductDto> mapper)
    : MServiceBase<Product, ProductDto>(context, mapper)
{
    // Hook override: set a site-specific default before a new entity is saved.
    protected override void ApplyDefaultValues(Product entity)
    {
        if (entity.Price <= 0)
        {
            entity.Price = 0.99m;
        }
    }
}
