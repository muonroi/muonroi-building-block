namespace Muonroi.Auth.Tests;

public class MAuthenticateTokenHelperTests
{
    private enum TestPerm
    {
        One = 1,
        Read = 2
    }

    [Fact]
    public void GenerateToken_Includes_TenantId_When_Enabled()
    {
        MTokenInfo info = new()
        {
            SymmetricSecretKey = "testkey123456789012345678901234567890",
            Issuer = "issuer",
            Audience = "audience",
            ExpiryMinutes = 60,
            MultiTenantEnabled = true,
            UseRsa = false
        };

        MAuthenticateTokenHelper<TestPerm> helper = new(
            info,
            new HmacTokenSigner(info.SymmetricSecretKey),
            new MDateTimeService());
        MUserModel user = new("guid", "user", "val", "name", "surname", "phone", "email", "tenant1");

        string token = helper.GenerateAuthenticateToken(user, [TestPerm.One]);
        JwtSecurityToken jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        jwt.Claims.FirstOrDefault(c => c.Type == ClaimConstants.TenantId)?.Value.Should().Be("tenant1");
    }

    [Fact]
    public void Generate_And_Validate_Token_Success()
    {
        MTokenInfo info = new()
        {
            SymmetricSecretKey = "testkey123456789012345678901234567890",
            Issuer = "issuer",
            Audience = "audience",
            ExpiryMinutes = 60,
            MultiTenantEnabled = false,
            UseRsa = false
        };

        MAuthenticateTokenHelper<TestPerm> helper = new(
            info,
            new HmacTokenSigner(info.SymmetricSecretKey),
            new MDateTimeService());
        MUserModel user = new("guid", "user", "val", "name", "surname", "phone", "email");

        string token = helper.GenerateAuthenticateToken(user, [TestPerm.One]);

        TokenValidationParameters parameters = new()
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = false,
            ValidIssuer = info.Issuer,
            ValidAudience = info.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(info.SymmetricSecretKey))
        };

        ClaimsPrincipal principal = new JwtSecurityTokenHandler().ValidateToken(token, parameters, out _);

        principal.FindFirst(ClaimConstants.UserIdentifier)?.Value.Should().Be("guid");
    }

    [Fact]
    public void Validate_Token_With_Wrong_Key_Fails()
    {
        MTokenInfo info = new()
        {
            SymmetricSecretKey = "testkey123456789012345678901234567890",
            Issuer = "issuer",
            Audience = "audience",
            ExpiryMinutes = 60,
            MultiTenantEnabled = false,
            UseRsa = false
        };

        MAuthenticateTokenHelper<TestPerm> helper = new(
            info,
            new HmacTokenSigner(info.SymmetricSecretKey),
            new MDateTimeService());
        MUserModel user = new("guid", "user", "val", "name", "surname", "phone", "email");

        string token = helper.GenerateAuthenticateToken(user, [TestPerm.One]);

        TokenValidationParameters parameters = new()
        {
            ValidateIssuerSigningKey = true,
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = false,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("wrongkeywrongkeywrongkeywrongkey"))
        };

        Action action = () => new JwtSecurityTokenHandler().ValidateToken(token, parameters, out _);

        action.Should().Throw<SecurityTokenException>();
    }

    [Fact]
    public void Validate_TenantSignedToken_WithKidResolver_Succeeds()
    {
        MTokenInfo info = new()
        {
            SymmetricSecretKey = "defaultkey1234567890123456789012345",
            Issuer = "issuer",
            Audience = "audience",
            ExpiryMinutes = 60,
            MultiTenantEnabled = true,
            UseRsa = false,
            SigningKeysByTenant = new Dictionary<string, string>
            {
                ["tenant1"] = "tenant1key123456789012345678901234"
            }
        };

        MAuthenticateTokenHelper<TestPerm> helper = new(
            info,
            new HmacTokenSigner(info.SymmetricSecretKey),
            new MDateTimeService());
        MUserModel user = new("guid", "user", "val", "name", "surname", "phone", "email", "tenant1");

        string token = helper.GenerateAuthenticateToken(user, [TestPerm.Read]);

        TokenValidationParameters parameters = new()
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = false,
            ValidIssuer = info.Issuer,
            ValidAudience = info.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(info.SymmetricSecretKey)),
            IssuerSigningKeyResolver = (_, _, kid, _) =>
            {
                if (!string.IsNullOrWhiteSpace(kid) && info.SigningKeysByTenant.TryGetValue(kid, out string? tenantKey))
                {
                    return
                    [
                        new SymmetricSecurityKey(Encoding.UTF8.GetBytes(tenantKey)) { KeyId = kid },
                        new SymmetricSecurityKey(Encoding.UTF8.GetBytes(info.SymmetricSecretKey))
                    ];
                }

                return [new SymmetricSecurityKey(Encoding.UTF8.GetBytes(info.SymmetricSecretKey))];
            }
        };

        ClaimsPrincipal principal = new JwtSecurityTokenHandler().ValidateToken(token, parameters, out _);

        principal.FindFirst(ClaimConstants.TenantId)?.Value.Should().Be("tenant1");
    }

    [Fact]
    public void Validate_TenantSignedToken_WithoutKidResolver_Fails()
    {
        MTokenInfo info = new()
        {
            SymmetricSecretKey = "defaultkey1234567890123456789012345",
            Issuer = "issuer",
            Audience = "audience",
            ExpiryMinutes = 60,
            MultiTenantEnabled = true,
            UseRsa = false,
            SigningKeysByTenant = new Dictionary<string, string>
            {
                ["tenant1"] = "tenant1key123456789012345678901234"
            }
        };

        MAuthenticateTokenHelper<TestPerm> helper = new(
            info,
            new HmacTokenSigner(info.SymmetricSecretKey),
            new MDateTimeService());
        MUserModel user = new("guid", "user", "val", "name", "surname", "phone", "email", "tenant1");

        string token = helper.GenerateAuthenticateToken(user, [TestPerm.Read]);

        TokenValidationParameters parameters = new()
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = false,
            ValidIssuer = info.Issuer,
            ValidAudience = info.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(info.SymmetricSecretKey))
        };

        Action action = () => new JwtSecurityTokenHandler().ValidateToken(token, parameters, out _);

        action.Should().Throw<SecurityTokenException>();
    }
}
