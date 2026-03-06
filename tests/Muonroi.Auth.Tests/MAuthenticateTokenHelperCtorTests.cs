namespace Muonroi.Auth.Tests;

public class MAuthenticateTokenHelperCtorTests
{
    private enum TestPerm
    {
        Read = 1
    }

    private static MTokenInfo CreateInfo()
    {
        return new MTokenInfo
        {
            SymmetricSecretKey = "testkey123456789012345678901234567890",
            Issuer = "issuer",
            Audience = "audience",
            ExpiryMinutes = 60,
            UseRsa = false
        };
    }

    [Fact]
    public void Ctor_Null_Config_Throws()
    {
        Action action = () => new MAuthenticateTokenHelper<TestPerm>(
            null!,
            new HmacTokenSigner("testkey123456789012345678901234567890"),
            new MDateTimeService());

        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Ctor_Null_Signer_Throws()
    {
        Action action = () => new MAuthenticateTokenHelper<TestPerm>(
            CreateInfo(),
            null!,
            new MDateTimeService());

        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Ctor_Valid_Inputs_Creates()
    {
        MAuthenticateTokenHelper<TestPerm> helper = new(
            CreateInfo(),
            new HmacTokenSigner("testkey123456789012345678901234567890"),
            new MDateTimeService());

        helper.Should().NotBeNull();
    }
}
