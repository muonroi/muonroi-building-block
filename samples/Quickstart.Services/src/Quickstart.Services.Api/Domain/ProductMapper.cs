using Muonroi.Mapping.Abstractions;

namespace Quickstart.Services.Api.Domain;

/// <summary>
/// Entity-DTO mapper required by MServiceBase. Derives from EntityMapperBase and
/// implements only the core field mapping; site-specific overrides are no-ops here.
/// See src/Muonroi.Mapping.Abstractions/EntityMapperBase.cs:10.
/// </summary>
public sealed class ProductMapper : EntityMapperBase<Product, ProductDto>
{
    protected override void MapCoreToDto(Product entity, ProductDto dto)
    {
        dto.Id = entity.Id;
        dto.Name = entity.Name;
        dto.Price = entity.Price;
    }

    protected override void MapCoreToEntity(ProductDto dto, Product entity)
    {
        // Id is managed by the store; map mutable fields only.
        entity.Name = dto.Name;
        entity.Price = dto.Price;
    }
}
