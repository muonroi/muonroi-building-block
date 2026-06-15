namespace Quickstart.Caching.Api.Models;

/// <summary>
/// Represents a product returned from the API.
/// CachedAt reflects when the object was constructed — useful for verifying cache hits vs. factory invocations.
/// </summary>
public record ProductDto(int Id, string Name, decimal Price, DateTime CachedAt);
