

namespace Muonroi.Auth.Keys;

/// <summary>
/// API Controller for serving the JSON Web Key Set (JWKS).
/// </summary>
/// <param name="jwtService">The service for JWT operations.</param>
[ApiController]
[Route(".well-known/jsonWebKeySet.json")]
public class JsonWebKeySetController(JwtService jwtService) : ControllerBase
{
    /// <summary>
    /// Retrieves the JSON Web Key Set containing the public keys for signature validation.
    /// </summary>
    /// <returns>The current JSON Web Key Set.</returns>
    [HttpGet]
    public JsonWebKeySet Get()
    {
        return jwtService.GetJsonWebKeySet();
    }
}