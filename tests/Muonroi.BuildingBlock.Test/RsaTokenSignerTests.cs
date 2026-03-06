namespace Muonroi.BuildingBlock.Test;

public class RsaTokenSignerTests
{
    [Fact]
    public void GetCredentials_Returns_RsaSecurityKey()
    {
        using RSA rsa = RSA.Create();
        RsaTokenSigner signer = new(rsa);
        SigningCredentials creds = signer.GetCredentials();
        Assert.IsType<RsaSecurityKey>(creds.Key);
    }

    [Fact]
    public void GetCredentials_Null_Rsa_Throws()
    {
        RsaTokenSigner signer = new(null!);
        Assert.ThrowsAny<Exception>(() => signer.GetCredentials());
    }
}
