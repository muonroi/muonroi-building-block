using Muonroi.Core.Abstractions.Interfaces;

namespace Quickstart.Mapper.Api.Models;

/// <summary>
/// Source domain entity.
/// </summary>
public sealed class Product
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
}

/// <summary>
/// Destination DTO. Implementing IMapFrom&lt;Product&gt; registers the
/// Product &lt;-&gt; ProductDto pair during ConfigureMapper() assembly scan.
/// See src/Muonroi.Core.Abstractions/Interfaces/IMapFrom.cs:7 and
/// src/Muonroi.Mapper/Mapper/MapperServiceCollectionExtensions.cs:31.
/// Mapping is performed by matching property names.
/// </summary>
public sealed class ProductDto : IMapFrom<Product>
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
}
