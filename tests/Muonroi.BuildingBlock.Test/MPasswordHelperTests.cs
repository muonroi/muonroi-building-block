namespace Muonroi.BuildingBlock.Test;

public class MPasswordHelperTests
{
    [Fact]
    public void HashPassword_GeneratesUniqueSaltPerCall()
    {
        string hash1 = MPasswordHelper.HashPassword("StrongPassword!123", out string? salt1);
        string hash2 = MPasswordHelper.HashPassword("StrongPassword!123", out string? salt2);

        Assert.NotEqual(salt1, salt2);
        Assert.NotEqual(hash1, hash2);
        Assert.Matches("^\\$2[aby]?\\$\\d{2}\\$[./A-Za-z0-9]{22}$", salt1);
        Assert.Matches("^\\$2[aby]?\\$\\d{2}\\$[./A-Za-z0-9]{22}$", salt2);
    }

    [Fact]
    public void VerifyPassword_ReturnsTrueForValidHash()
    {
        const string password = "Another$trongPass1";
        string hash = MPasswordHelper.HashPassword(password, out string? salt);

        Assert.True(MPasswordHelper.VerifyPassword(password, hash));
        Assert.Matches("^\\$2[aby]?\\$\\d{2}\\$[./A-Za-z0-9]{22}$", salt);
    }

    [Fact]
    public void VerifyPassword_ReturnsFalseForInvalidPassword()
    {
        const string password = "StrongPassword!123";
        string hash = MPasswordHelper.HashPassword(password, out _);

        Assert.False(MPasswordHelper.VerifyPassword("WrongPassword", hash));
    }
}
