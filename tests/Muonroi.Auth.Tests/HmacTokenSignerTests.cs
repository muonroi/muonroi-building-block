namespace Muonroi.Auth.Tests;

public class HmacTokenSignerTests
{
    [Fact]
    public void GetCredentials_ReturnsHmacSha256()
    {
        var signer = new HmacTokenSigner("this-is-a-secret-key-at-least-32-characters");

        var creds = signer.GetCredentials();

        creds.Should().NotBeNull();
        creds.Algorithm.Should().Be(SecurityAlgorithms.HmacSha256);
    }

    [Fact]
    public void GetCredentials_ConsistentForSameKey()
    {
        string key = "consistent-secret-key-with-length";
        var signer = new HmacTokenSigner(key);

        var creds1 = signer.GetCredentials();
        var creds2 = signer.GetCredentials();

        creds1.Algorithm.Should().Be(creds2.Algorithm);
    }
}
