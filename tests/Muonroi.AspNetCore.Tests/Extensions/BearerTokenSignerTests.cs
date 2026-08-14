namespace Muonroi.AspNetCore.Tests.Extensions;

/// <summary>
/// Tests that AddValidateBearerToken registers internal signer implementations
/// without requiring the Muonroi.Auth package.
/// </summary>
public class BearerTokenSignerTests
{
    [Fact]
    public void AddValidateBearerToken_WithHmacKey_ResolvesInternalHmacSigner()
    {
        // Arrange
        var services = new ServiceCollection();
        var config = BuildMinimalConfig(useRsa: false, hmacKey: "test-key-at-least-32-characters-long!");

        var tokenConfig = new MTokenInfo
        {
            Issuer = "test-issuer",
            Audience = "test-audience",
            SymmetricSecretKey = "test-key-at-least-32-characters-long!",
            UseRsa = false,
            ExpiryMinutes = 60
        };
        services.AddSingleton(tokenConfig);

        // Act
        services.AddValidateBearerToken(config);

        // Assert
        using var sp = services.BuildServiceProvider();
        var signer = sp.GetService<ITokenSigner>();
        signer.Should().NotBeNull();

        var creds = signer!.GetCredentials();
        creds.Algorithm.Should().Be(SecurityAlgorithms.HmacSha256);
        creds.Key.Should().BeOfType<SymmetricSecurityKey>();
    }

    [Fact]
    public void AddValidateBearerToken_WithRsaKey_ResolvesInternalRsaSigner()
    {
        // Arrange
        var services = new ServiceCollection();

        // Generate a temporary RSA key for testing
        using var rsa = System.Security.Cryptography.RSA.Create(2048);
        string pemKey = rsa.ExportRSAPrivateKeyPem();

        var tokenConfig = new MTokenInfo
        {
            Issuer = "test-issuer",
            Audience = "test-audience",
            UseRsa = true,
            PrivateKey = pemKey,
            ExpiryMinutes = 60
        };
        services.AddSingleton(tokenConfig);

        var config = BuildMinimalConfig(useRsa: true);

        // Act
        services.AddValidateBearerToken(config);

        // Assert
        using var sp = services.BuildServiceProvider();
        var signer = sp.GetService<ITokenSigner>();
        signer.Should().NotBeNull();

        var creds = signer!.GetCredentials();
        creds.Algorithm.Should().Be(SecurityAlgorithms.RsaSha256);
        creds.Key.Should().BeOfType<RsaSecurityKey>();
    }

    [Fact]
    public void AddValidateBearerToken_WithoutAuthRegistered_SignerResolvesSuccessfully()
    {
        // Arrange — no Auth package types in DI at all
        var services = new ServiceCollection();
        var tokenConfig = new MTokenInfo
        {
            Issuer = "test-issuer",
            Audience = "test-audience",
            SymmetricSecretKey = "test-key-at-least-32-characters-long!",
            UseRsa = false,
            ExpiryMinutes = 60
        };
        services.AddSingleton(tokenConfig);

        var config = BuildMinimalConfig(useRsa: false, hmacKey: "test-key-at-least-32-characters-long!");

        // Act
        services.AddValidateBearerToken(config);

        // Assert — ITokenSigner resolves to internal implementation
        using var sp = services.BuildServiceProvider();
        var signer = sp.GetRequiredService<ITokenSigner>();
        signer.Should().NotBeNull();
        signer.GetType().Name.Should().Contain("TokenSigner");
    }

    private static IConfiguration BuildMinimalConfig(bool useRsa = false, string hmacKey = "")
    {
        var dict = new Dictionary<string, string?>
        {
            ["TokenConfigs:Issuer"] = "test-issuer",
            ["TokenConfigs:Audience"] = "test-audience",
            ["TokenConfigs:UseRsa"] = useRsa.ToString(),
            ["TokenConfigs:SymmetricSecretKey"] = hmacKey,
            ["TokenConfigs:ExpiryMinutes"] = "60"
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(dict)
            .Build();
    }
}
