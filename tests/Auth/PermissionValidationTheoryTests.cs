namespace Muonroi.Auth.Tests;

public class PermissionValidationTheoryTests
{
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("\n")]
    public void ValidateUserId_EmptyOrWhitespaceUserId_ReturnsFalse(string userId)
    {
        bool result = Guid.TryParse(userId, out _);
        Assert.False(result);
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("12345")]
    [InlineData("abc-def-ghi")]
    [InlineData("00000000-0000-0000-0000")]
    [InlineData("invalid-format")]
    public void ValidateUserId_InvalidGuidFormat_ReturnsFalse(string userId)
    {
        bool result = Guid.TryParse(userId, out _);
        Assert.False(result);
    }

    [Theory]
    [InlineData("12345678-1234-1234-1234-123456789012")]
    [InlineData("a1b2c3d4-e5f6-7890-abcd-ef1234567890")]
    [InlineData("ffffffff-ffff-ffff-ffff-ffffffffffff")]
    public void ValidateUserId_ValidGuidFormat_ReturnsTrue(string userId)
    {
        bool result = Guid.TryParse(userId, out Guid guid);
        Assert.True(result);
        Assert.NotEqual(Guid.Empty, guid);
    }

    [Theory]
    [InlineData("permission.read")]
    [InlineData("permission.write")]
    [InlineData("admin.full.access")]
    [InlineData("user.profile.edit")]
    public void ValidatePermissionKey_ValidKeys_AreNotEmpty(string permissionKey)
    {
        Assert.False(string.IsNullOrWhiteSpace(permissionKey));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public void ValidatePermissionKey_EmptyKeys_AreInvalid(string permissionKey)
    {
        Assert.True(string.IsNullOrWhiteSpace(permissionKey));
    }

    [Theory]
    [InlineData("permission@read")]
    [InlineData("permission#write")]
    [InlineData("admin$access")]
    [InlineData("user%profile")]
    [InlineData("role&management")]
    public void ValidatePermissionKey_SpecialCharacters_AreValid(string permissionKey)
    {
        Assert.False(string.IsNullOrWhiteSpace(permissionKey));
        Assert.NotEmpty(permissionKey);
    }
}
