namespace Muonroi.Auth.Tests;

public class RsaTokenSignerTests
{
    [Fact]
    public void GetCredentials_Returns_RsaSecurityKey()
    {
        using RSA rsa = RSA.Create();
        RsaTokenSigner signer = new(rsa);

        SigningCredentials credentials = signer.GetCredentials();

        credentials.Key.Should().BeOfType<RsaSecurityKey>();
        credentials.Algorithm.Should().Be(SecurityAlgorithms.RsaSha256);
    }

    [Fact]
    public void GetCredentials_Null_Rsa_Throws()
    {
        RsaTokenSigner signer = new(null!);

        Action action = () => signer.GetCredentials();

        action.Should().Throw<Exception>();
    }
}
