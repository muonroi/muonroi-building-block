namespace Muonroi.Auth.Jwt;

/// <summary>
/// Service for generating and validating JSON Web Tokens (JWTs).
/// </summary>
[SuppressMessage("Muonroi.CodeStandards", "MSTD0001", Justification = "JWT validation contract: SecurityTokenException is the type the JWT bearer pipeline expects.")]
public class JwtService
{
    private readonly IRsaKeyStore _keyStore;
    private readonly ITokenRevocationStore _revocation;
    private readonly IMDateTimeService _dateTimeService;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly JwtSecurityTokenHandler _handler = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="JwtService"/> class.
    /// </summary>
    /// <param name="keyStore">The RSA key store for signing.</param>
    /// <param name="revocation">The token revocation store.</param>
    /// <param name="issuer">The token issuer.</param>
    /// <param name="audience">The token audience.</param>
    /// <param name="dateTimeService">Service to retrieve the current date and time.</param>
    public JwtService(IRsaKeyStore keyStore, ITokenRevocationStore revocation, string issuer, string audience, IMDateTimeService dateTimeService)
    {
        _keyStore = keyStore;
        _revocation = revocation;
        _dateTimeService = dateTimeService;
        _issuer = issuer;
        _audience = audience;
        _handler.InboundClaimTypeMap.Clear();
        _handler.OutboundClaimTypeMap.Clear();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JwtService"/> class using token information.
    /// </summary>
    /// <param name="keyStore">The RSA key store for signing.</param>
    /// <param name="revocation">The token revocation store.</param>
    /// <param name="tokenInfo">The token configuration info.</param>
    /// <param name="dateTimeService">Service to retrieve the current date and time.</param>
    public JwtService(IRsaKeyStore keyStore, ITokenRevocationStore revocation, MTokenInfo tokenInfo, IMDateTimeService dateTimeService)
        : this(keyStore, revocation, tokenInfo.Issuer, tokenInfo.Audience, dateTimeService)
    {
    }

    /// <summary>
    /// Generates a signed JWT for a specific subject.
    /// </summary>
    /// <param name="subject">The subject (sub) claim for the token.</param>
    /// <param name="lifetime">The duration for which the token remains valid.</param>
    /// <param name="notBefore">Optional date and time before which the token is not valid.</param>
    /// <returns>A serialized JWT string.</returns>
    public string GenerateToken(string subject, TimeSpan lifetime, DateTime? notBefore = null)
    {
        DateTime now = _dateTimeService.UtcNow();
        SigningCredentials signingCredentials = _keyStore.GetCurrentSigningCredentials();
        List<Claim> claims =
        [
            new(JwtRegisteredClaimNames.Sub, subject),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        ];
        DateTime notBeforeDate = notBefore ?? now;
        SecurityTokenDescriptor descriptor = new()
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = _issuer,
            Audience = _audience,
            NotBefore = notBeforeDate,
            Expires = notBeforeDate.Add(lifetime),
            IssuedAt = now,
            SigningCredentials = signingCredentials
        };
        JwtSecurityToken token = _handler.CreateJwtSecurityToken(descriptor);
        token.Header[JwtHeaderParameterNames.Kid] = signingCredentials.Kid;
        return _handler.WriteToken(token);
    }

    /// <summary>
    /// Validates a JWT string and returns the claims principal if successful.
    /// </summary>
    /// <param name="token">The JWT string to validate.</param>
    /// <returns>The <see cref="ClaimsPrincipal"/> derived from the token.</returns>
    /// <exception cref="SecurityTokenException">Thrown if the token is invalid or revoked.</exception>
    public ClaimsPrincipal ValidateToken(string token)
    {
        TokenValidationParameters parameters = new()
        {
            RequireSignedTokens = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeyResolver = (_, _, kid, _) =>
                {
                    SecurityKey? key = _keyStore.GetKey(kid);
                    return key != null ? [key] : [];
                },
            ValidateIssuer = true,
            ValidIssuer = _issuer,
            ValidateAudience = true,
            ValidAudience = _audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256]
        };

        ClaimsPrincipal principal = _handler.ValidateToken(token, parameters, out SecurityToken? securityToken);
        if (securityToken is not JwtSecurityToken jwt)
        {
            return principal;
        }

        if (jwt.Header.Alg != SecurityAlgorithms.RsaSha256)
        {
            throw new SecurityTokenException("Invalid alg");
        }

        return _revocation.IsRevoked(jwt.Id) ? throw new SecurityTokenException("Token revoked") : principal;
    }

    /// <summary>
    /// Forces a rotation of the RSA signing keys.
    /// </summary>
    public void RotateKeys()
    {
        _keyStore.RotateKeys();
    }

    /// <summary>
    /// Revokes a specific JWT.
    /// </summary>
    /// <param name="token">The JWT string to revoke.</param>
    public void RevokeToken(string token)
    {
        JwtSecurityToken jwt = _handler.ReadJwtToken(token);
        _revocation.Revoke(jwt.Id, jwt.ValidTo);
    }

    /// <summary>
    /// Gets the current JSON Web Key Set (JWKS) containing the public keys.
    /// </summary>
    /// <returns>The set of JSON Web Keys.</returns>
    public JsonWebKeySet GetJsonWebKeySet()
    {
        return _keyStore.GetJsonWebKeySet();
    }
}
