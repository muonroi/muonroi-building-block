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

        claim.Value.Should().Be(value);
        claim.Type.Should().Be(claimType);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public void Claim_WithEmptyValue_StillCreates(string emptyValue)
    {
        Claim claim = new("TestType", emptyValue);

        claim.Value.Should().Be(emptyValue);
    }

    [Theory]
    [InlineData("user@example.com")]
    [InlineData("user+tag@example.com")]
    [InlineData("user.name@example.com")]
    [InlineData("user_123@example.com")]
    public void Claim_WithEmailFormat_CreatesSuccessfully(string email)
    {
        Claim claim = new(ClaimTypes.Email, email);

        claim.Value.Should().Be(email);
        claim.Type.Should().Be(ClaimTypes.Email);
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("12345678-1234-1234-1234-123456789012")]
    [InlineData("a1b2c3d4-e5f6-7890-abcd-ef1234567890")]
    public void Claim_WithGuidValue_CreatesSuccessfully(string guidValue)
    {
        Claim claim = new("UserId", guidValue);

        claim.Value.Should().Be(guidValue);
        Guid.TryParse(claim.Value, out _).Should().BeTrue();
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("user")]
    [InlineData("moderator")]
    [InlineData("guest")]
    public void Claim_WithRoleValue_CreatesSuccessfully(string role)
    {
        Claim claim = new(ClaimTypes.Role, role);

        claim.Value.Should().Be(role);
        claim.Type.Should().Be(ClaimTypes.Role);
    }

    [Theory]
    [InlineData("special@chars")]
    [InlineData("unicode_a")]
    [InlineData("emoji_test")]
    [InlineData("multi\nline")]
    public void Claim_WithSpecialCharacters_CreatesSuccessfully(string specialValue)
    {
        Claim claim = new("SpecialClaim", specialValue);

        claim.Value.Should().Be(specialValue);
    }

    [Theory]
    [InlineData(ClaimTypes.Name)]
    [InlineData(ClaimTypes.Email)]
    [InlineData(ClaimTypes.Role)]
    [InlineData(ClaimTypes.NameIdentifier)]
    public void Claim_WithStandardClaimTypes_CreatesSuccessfully(string claimType)
    {
        Claim claim = new(claimType, "test-value");

        claim.Type.Should().Be(claimType);
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

        identity.Claims.Should().HaveCount(claimCount);
    }

    [Fact]
    public void ClaimsPrincipal_FindFirst_ReturnsCorrectClaim()
    {
        ClaimsPrincipal principal = new(new ClaimsIdentity(
        [
            new Claim("TestClaim", "TestValue"),
            new Claim("OtherClaim", "OtherValue")
        ]));

        Claim? foundClaim = principal.FindFirst("TestClaim");

        foundClaim.Should().NotBeNull();
        foundClaim!.Value.Should().Be("TestValue");
    }

    [Theory]
    [InlineData("NonExistentClaim")]
    [InlineData("MissingClaim")]
    [InlineData("")]
    public void ClaimsPrincipal_FindFirst_WithMissingClaim_ReturnsNull(string claimType)
    {
        ClaimsPrincipal principal = new(new ClaimsIdentity([new Claim("ExistingClaim", "Value")]));

        Claim? foundClaim = principal.FindFirst(claimType);

        foundClaim.Should().BeNull();
    }
}
