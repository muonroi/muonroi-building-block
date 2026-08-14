namespace Quickstart.Services.Api.Domain;

/// <summary>
/// Demo entity. Implements IEntityBase&lt;long&gt; so MServiceBase can operate on it
/// generically via DbContext.Set&lt;Product&gt;().
/// See src/Muonroi.Data.Abstractions/Entities/IEntityBase.cs:16.
/// </summary>
public sealed class Product : IEntityBase<long>
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

/// <summary>DTO exposed by the API.</summary>
public sealed class ProductDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}
