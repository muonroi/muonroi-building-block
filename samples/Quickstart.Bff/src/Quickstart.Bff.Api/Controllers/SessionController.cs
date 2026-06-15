using Microsoft.AspNetCore.Mvc;
using Muonroi.Bff;
using Quickstart.Bff.Api.Models;

namespace Quickstart.Bff.Api.Controllers;

/// <summary>
/// Demonstrates server-side refresh-token handling via ITokenStore.
/// In a real BFF the refresh token is issued during the OIDC code exchange and
/// stored here keyed by subject; the browser only ever sees the auth cookie.
/// </summary>
[ApiController]
[Route("api/session")]
public sealed class SessionController(ITokenStore tokenStore) : ControllerBase
{
    // POST api/session/refresh-token
    // Stores a refresh token for a subject (server-side only).
    [HttpPost("refresh-token")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Store([FromBody] StoreTokenRequest request)
    {
        await tokenStore.StoreRefreshTokenAsync(request.Subject, request.RefreshToken);
        return NoContent();
    }

    // GET api/session/refresh-token/{subject}
    // Retrieves the stored refresh token for a subject.
    [HttpGet("refresh-token/{subject}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(string subject)
    {
        string? token = await tokenStore.GetRefreshTokenAsync(subject);
        return token is null ? NotFound() : Ok(new { subject, refreshToken = token });
    }

    // DELETE api/session/refresh-token/{subject}
    // Removes the stored refresh token (e.g. on logout).
    [HttpDelete("refresh-token/{subject}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Remove(string subject)
    {
        await tokenStore.RemoveRefreshTokenAsync(subject);
        return NoContent();
    }
}
