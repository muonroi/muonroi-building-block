namespace Quickstart.Http.Api.Models;

/// <summary>
/// Shape of a post returned by the demo upstream API (jsonplaceholder).
/// </summary>
public record PostDto(int UserId, int Id, string Title, string Body);
