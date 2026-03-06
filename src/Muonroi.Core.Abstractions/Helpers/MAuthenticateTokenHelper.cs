namespace Muonroi.Core.Abstractions.Helpers;

public class MAuthenticateTokenHelper<TPermission>(
    MTokenInfo tokenConfig,
    ITokenSigner signer,
    IMDateTimeService dateTimeService,
    ILogger<MAuthenticateTokenHelper<TPermission>>? logger = null)
    where TPermission : Enum
{
    private readonly MTokenInfo _tokenConfig = tokenConfig ?? throw new ArgumentNullException(nameof(tokenConfig));
    private readonly ITokenSigner _signer = signer ?? throw new ArgumentNullException(nameof(signer));

    public string GenerateAuthenticateToken(MUserModel user, List<TPermission> permissions, List<Claim>? claims = null)
    {
        ArgumentNullException.ThrowIfNull(permissions);
        logger?.LogInformation("Generating token for {User}", user.UserGuid);
        List<Claim> privateClaims =
        [
            new(ClaimConstants.Username, user.Username),
            new(ClaimConstants.UserIdentifier, user.UserGuid)
        ];

        string? tenantId = null;
        if (_tokenConfig.MultiTenantEnabled)
        {
            tenantId = user.TenantId ?? _tokenConfig.TenantId;
            if (!string.IsNullOrWhiteSpace(tenantId))
            {
                privateClaims.Add(new Claim(ClaimConstants.TenantId, tenantId));
            }
        }

        if (claims is not null)
        {
            privateClaims.AddRange(claims);
        }

        SigningCredentials baseCreds = _signer.GetCredentials();
        SecurityKey key = baseCreds.Key;
        string algorithm = baseCreds.Algorithm;

        if (!string.IsNullOrWhiteSpace(tenantId) &&
            _tokenConfig.SigningKeysByTenant.TryGetValue(tenantId, out string? tenantKey) && !_tokenConfig.UseRsa)
        {
            key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(tenantKey));
            algorithm = SecurityAlgorithms.HmacSha256;
        }

        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            key.KeyId = tenantId;
        }

        SigningCredentials credentials = new(key, algorithm);

        SecurityTokenDescriptor tokenDescriptor = new()
        {
            Issuer = _tokenConfig.Issuer,
            Audience = _tokenConfig.Audience,
            Subject = new ClaimsIdentity(privateClaims),
            Expires = dateTimeService.UtcNow().AddMinutes(_tokenConfig.ExpiryMinutes),
            SigningCredentials = credentials
        };

        JwtSecurityTokenHandler tokenHandler = new();
        SecurityToken token = tokenHandler.CreateToken(tokenDescriptor);
        string result = tokenHandler.WriteToken(token);
        logger?.LogInformation("Generated token for {User}", user.UserGuid);
        return result;
    }
}
