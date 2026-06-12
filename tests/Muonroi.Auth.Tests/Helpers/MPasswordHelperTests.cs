namespace Muonroi.Auth.Tests.Helpers;

public class MPasswordHelperTests
{
    [Fact]
    public void HashPassword_Generates_Unique_Salt_Per_Call()
    {
        string hash1 = MPasswordHelper.HashPassword("StrongPassword!123", out string salt1);
        string hash2 = MPasswordHelper.HashPassword("StrongPassword!123", out string salt2);

        salt1.Should().NotBe(salt2);
        hash1.Should().NotBe(hash2);
        salt1.Should().MatchRegex("^\\$2[aby]?\\$\\d{2}\\$[./A-Za-z0-9]{22}$");
        salt2.Should().MatchRegex("^\\$2[aby]?\\$\\d{2}\\$[./A-Za-z0-9]{22}$");
    }

    [Fact]
    public void VerifyPassword_Returns_True_For_Valid_Hash()
    {
        const string password = "Another$trongPass1";
        string hash = MPasswordHelper.HashPassword(password, out string salt);

        MPasswordHelper.VerifyPassword(password, hash).Should().BeTrue();
        salt.Should().MatchRegex("^\\$2[aby]?\\$\\d{2}\\$[./A-Za-z0-9]{22}$");
    }

    [Fact]
    public void VerifyPassword_Returns_False_For_Invalid_Password()
    {
        string hash = MPasswordHelper.HashPassword("StrongPassword!123", out _);

        MPasswordHelper.VerifyPassword("WrongPassword", hash).Should().BeFalse();
    }
}
