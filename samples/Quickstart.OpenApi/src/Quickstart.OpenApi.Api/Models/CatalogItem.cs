namespace Quickstart.OpenApi.Api.Models;

/// <summary>
/// A catalog item exposed through the documented quickstart endpoints.
/// </summary>
public record CatalogItem(int Id, string Name, decimal Price);
