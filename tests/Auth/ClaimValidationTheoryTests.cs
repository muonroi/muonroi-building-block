using System.Security.Claims;
using Xunit;

namespace Muonroi.Auth.Tests;

public class ClaimValidationTheoryTests
{
    [Theory]
    [InlineData("user123", "UserIdentifier")]
    [InlineData("admin@example.com", "Email")]
    [InlineData("John Doe", "Name")]
    [InlineData("role-admin", "Role")]
    public void Claim_WithValidValues_CreatesSuccessfully(string value, string claimType)
    {
        Claim claim = new(claimType, value);

        Assert.Equal(value, claim.Value);
        Assert.Equal(claimType, claim.Type);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public void Claim_WithEmptyValue_StillCreates(string emptyValue)
    {
        Claim claim = new("TestType", emptyValue);

        Assert.Equal(emptyValue, claim.Value);
    }

    [Theory]
    [InlineData("user@example.com")]
    [InlineData("user+tag@example.com")]
    [InlineData("user.name@example.com")]
    [InlineData("user_123@example.com")]
    public void Claim_WithEmailFormat_CreatesSuccessfully(string email)
    {
        Claim claim = new(ClaimTypes.Email, email);

        Assert.Equal(email, claim.Value);
        Assert.Equal(ClaimTypes.Email, claim.Type);
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("12345678-1234-1234-1234-123456789012")]
    [InlineData("a1b2c3d4-e5f6-7890-abcd-ef1234567890")]
    public void Claim_WithGuidValue_CreatesSuccessfully(string guidValue)
    {
        Claim claim = new("UserId", guidValue);

        Assert.Equal(guidValue, claim.Value);
        Assert.True(Guid.TryParse(claim.Value, out _));
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("user")]
    [InlineData("moderator")]
    [InlineData("guest")]
    public void Claim_WithRoleValue_CreatesSuccessfully(string role)
    {
        Claim claim = new(ClaimTypes.Role, role);

        Assert.Equal(role, claim.Value);
        Assert.Equal(ClaimTypes.Role, claim.Type);
    }

    [Theory]
    [InlineData("special@chars")]
    [InlineData("unicode_中文")]
    [InlineData("emoji_😀")]
    [InlineData("multi\nline")]
    public void Claim_WithSpecialCharacters_CreatesSuccessfully(string specialValue)
    {
        Claim claim = new("SpecialClaim", specialValue);

        Assert.Equal(specialValue, claim.Value);
    }

    [Theory]
    [InlineData(ClaimTypes.Name)]
    [InlineData(ClaimTypes.Email)]
    [InlineData(ClaimTypes.Role)]
    [InlineData(ClaimTypes.NameIdentifier)]
    public void Claim_WithStandardClaimTypes_CreatesSuccessfully(string claimType)
    {
        Claim claim = new(claimType, "test-value");

        Assert.Equal(claimType, claim.Type);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    public void ClaimsIdentity_WithMultipleClaims_StoresAllClaims(int claimCount)
    {
        List<Claim> claims = [];
        for (int i = 0; i < claimCount; i++)
        {
            claims.Add(new Claim($"Claim{i}", $"Value{i}"));
        }

        ClaimsIdentity identity = new(claims);

        Assert.Equal(claimCount, identity.Claims.Count());
    }

    [Fact]
    public void ClaimsPrincipal_FindFirst_ReturnsCorrectClaim()
    {
        Claim[] claims = new[]
        {
            new Claim("TestClaim", "TestValue"),
            new Claim("OtherClaim", "OtherValue")
        };
        ClaimsIdentity identity = new(claims);
        ClaimsPrincipal principal = new(identity);

        Claim? foundClaim = principal.FindFirst("TestClaim");

        Assert.NotNull(foundClaim);
        Assert.Equal("TestValue", foundClaim.Value);
    }

    [Theory]
    [InlineData("NonExistentClaim")]
    [InlineData("MissingClaim")]
    [InlineData("")]
    public void ClaimsPrincipal_FindFirst_WithMissingClaim_ReturnsNull(string claimType)
    {
        Claim[] claims = new[] { new Claim("ExistingClaim", "Value") };
        ClaimsIdentity identity = new(claims);
        ClaimsPrincipal principal = new(identity);

        Claim? foundClaim = principal.FindFirst(claimType);

        Assert.Null(foundClaim);
    }
}
