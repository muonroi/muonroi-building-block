using Microsoft.AspNetCore.Mvc;
using Quickstart.Http.Api.Models;
using Quickstart.Http.Api.Services;

namespace Quickstart.Http.Api.Controllers;

/// <summary>
/// Calls an upstream API through the Muonroi.Http BaseApiService-derived
/// <see cref="JsonPlaceholderClient"/>, exercising the resilient SendAsync pipeline
/// and the correlation/auth DelegatingHandlers attached to the named client.
/// </summary>
[ApiController]
[Route("api/posts")]
public sealed class PostsController(JsonPlaceholderClient client) : ControllerBase
{
    // GET api/posts/{id}
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(PostDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(int id, CancellationToken cancellationToken)
    {
        PostDto post = await client.GetPostAsync(id, cancellationToken);
        return Ok(post);
    }
}
