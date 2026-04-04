using Muonroi.Core.Abstractions.Exceptions;
namespace Muonroi.BuildingBlock.Test;

public class MAuthenticateTokenHelperCtorTests
{
    private static MTokenInfo CreateInfo()
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
        return info;
    }

    [Fact]
    public void Ctor_Null_Config_Throws()
    {
        ITokenSigner signer = Substitute.For<ITokenSigner>();
        Assert.Throws<MArgumentException>(() => new MAuthenticateTokenHelper<TestPerm>(null!, signer));
    }

    [Fact]
    public void Ctor_Null_Signer_Throws()
    {
        MTokenInfo info = CreateInfo();
        Assert.Throws<MArgumentException>(() => new MAuthenticateTokenHelper<TestPerm>(info, null!));
    }

    [Fact]
    public void Ctor_Valid_Inputs_Creates()
    {
        MTokenInfo info = CreateInfo();
        MAuthenticateTokenHelper<TestPerm> helper = new(info, new HmacTokenSigner(info.SymmetricSecretKey));
        Assert.NotNull(helper);
    }
}

