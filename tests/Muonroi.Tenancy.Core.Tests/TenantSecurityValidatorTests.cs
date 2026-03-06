namespace Muonroi.Tenancy.Core.Tests;

public class TenantSecurityValidatorTests
{
    [Fact]
    public void TryValidate_Returns_False_When_Context_Is_Missing()
    {
        bool isValid = TenantSecurityValidator.TryValidate(null, "tenant-a", "tenant-a", true, out string errorCode);

        isValid.Should().BeFalse();
        errorCode.Should().Be(TenantSecurityValidator.MissingTenantContext);
    }

    [Fact]
    public void TryValidate_Returns_False_When_Claim_Is_Required_But_Missing()
    {
        bool isValid = TenantSecurityValidator.TryValidate("tenant-a", null, null, true, out string errorCode);

        isValid.Should().BeFalse();
        errorCode.Should().Be(TenantSecurityValidator.MissingTenantClaim);
    }

    [Fact]
    public void TryValidate_Returns_True_For_Matching_Context_Claim_And_Header()
    {
        bool isValid = TenantSecurityValidator.TryValidate(" tenant-a ", "tenant-a", "tenant-a", true, out string errorCode);

        isValid.Should().BeTrue();
        errorCode.Should().BeEmpty();
    }

    [Fact]
    public void SecurityNamespace_Validator_Returns_HeaderClaimMismatch()
    {
        bool isValid = Security.TenantSecurityValidator.TryValidate("tenant-a", "tenant-a", "tenant-b", true, out string? errorCode);

        isValid.Should().BeFalse();
        errorCode.Should().Be(Security.TenantSecurityValidator.HeaderClaimMismatch);
    }
}
