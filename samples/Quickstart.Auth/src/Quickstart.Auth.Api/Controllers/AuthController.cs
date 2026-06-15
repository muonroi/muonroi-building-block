using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Muonroi.Auth.Jwt;
using Muonroi.Core.Abstractions.Interfaces;
using Quickstart.Auth.Api.Models;

namespace Quickstart.Auth.Api.Controllers;

/// <summary>
/// Exercises the primary public surface of Muonroi.Auth:
///   - JwtService.GenerateToken / ValidateToken / RevokeToken / RotateKeys / GetJsonWebKeySet
///   - IPasswordHasher (BCryptPasswordHasher) HashPassword / VerifyPassword
///
/// JwtService and IPasswordHasher are registered by AddInMemoryRsaKeyStore().
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthController(JwtService jwt, IPasswordHasher passwordHasher) : ControllerBase
{
    // ---------------------------------------------------------------------------
    // 1. Issue a signed RS256 JWT
    //    POST /api/auth/token
    //
    //    GenerateToken signs the token with the current RSA key from IRsaKeyStore
    //    and stamps the kid header so ValidateToken can resolve the public key.
    // ---------------------------------------------------------------------------
    [HttpPost("token")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult IssueToken([FromBody] TokenRequest request)
    {
        string token = jwt.GenerateToken(
            subject: request.Subject,
            lifetime: TimeSpan.FromMinutes(request.LifetimeMinutes));

        return Ok(new { token, request.Subject, expiresInMinutes = request.LifetimeMinutes });
    }

    // ---------------------------------------------------------------------------
    // 2. Validate a JWT
    //    POST /api/auth/validate
    //
    //    ValidateToken verifies the signature against the JWKS, checks issuer /
    //    audience / lifetime, enforces RS256, and rejects revoked tokens.
    // ---------------------------------------------------------------------------
    [HttpPost("validate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult ValidateToken([FromBody] TokenEnvelope envelope)
    {
        try
        {
            ClaimsPrincipal principal = jwt.ValidateToken(envelope.Token);
            IEnumerable<object> claims = principal.Claims.Select(c => new { c.Type, c.Value });
            return Ok(new { valid = true, claims });
        }
        catch (SecurityTokenException ex)
        {
            // No silent catch — surface the validation failure reason to the caller.
            return Unauthorized(new { valid = false, reason = ex.Message });
        }
    }

    // ---------------------------------------------------------------------------
    // 3. Revoke a JWT
    //    POST /api/auth/revoke
    //
    //    RevokeToken records the token's jti in ITokenRevocationStore. Subsequent
    //    ValidateToken calls for the same token throw "Token revoked".
    // ---------------------------------------------------------------------------
    [HttpPost("revoke")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult RevokeToken([FromBody] TokenEnvelope envelope)
    {
        jwt.RevokeToken(envelope.Token);
        return Ok(new { message = "Token revoked. ValidateToken will now reject it." });
    }

    // ---------------------------------------------------------------------------
    // 4. JWKS export
    //    GET /api/auth/.well-known/jwks
    //
    //    GetJsonWebKeySet returns the public keys clients use to verify tokens
    //    offline. Mirror the JwksController shipped in the package.
    // ---------------------------------------------------------------------------
    [HttpGet(".well-known/jwks")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetJwks()
    {
        JsonWebKeySet jwks = jwt.GetJsonWebKeySet();
        return Ok(jwks);
    }

    // ---------------------------------------------------------------------------
    // 5. RSA key rotation
    //    POST /api/auth/rotate-keys
    //
    //    RotateKeys promotes a fresh signing key. Tokens issued before rotation
    //    still validate because the previous public key remains in the JWKS.
    // ---------------------------------------------------------------------------
    [HttpPost("rotate-keys")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult RotateKeys()
    {
        jwt.RotateKeys();
        return Ok(new { message = "Signing keys rotated. New tokens use the new key; old keys remain in JWKS." });
    }

    // ---------------------------------------------------------------------------
    // 6. BCrypt password hashing
    //    POST /api/auth/password
    //
    //    IPasswordHasher (BCryptPasswordHasher) hashes the password with a
    //    generated salt and immediately verifies it round-trips.
    // ---------------------------------------------------------------------------
    [HttpPost("password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult HashAndVerify([FromBody] PasswordRequest request)
    {
        string hash = passwordHasher.HashPassword(request.Password, out string salt);
        bool verified = passwordHasher.VerifyPassword(request.Password, hash);

        return Ok(new
        {
            hash,
            salt,
            verified,
            note = "Salt is embedded in the BCrypt hash; store only the hash."
        });
    }
}
