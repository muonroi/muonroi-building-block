namespace Muonroi.Auth.Jwt;

public class JwtService
{
    private readonly IRsaKeyStore _keyStore;
    private readonly ITokenRevocationStore _revocation;
    private readonly IMDateTimeService _dateTimeService;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly JwtSecurityTokenHandler _handler = new();

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

    public JwtService(IRsaKeyStore keyStore, ITokenRevocationStore revocation, MTokenInfo tokenInfo, IMDateTimeService dateTimeService)
        : this(keyStore, revocation, tokenInfo.Issuer, tokenInfo.Audience, dateTimeService)
    {
    }

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

    public void RotateKeys()
    {
        _keyStore.RotateKeys();
    }

    public void RevokeToken(string token)
    {
        JwtSecurityToken jwt = _handler.ReadJwtToken(token);
        _revocation.Revoke(jwt.Id, jwt.ValidTo);
    }

    public JsonWebKeySet GetJsonWebKeySet()
    {
        return _keyStore.GetJsonWebKeySet();
    }
}
