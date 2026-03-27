namespace Muonroi.Governance.Tests;

public class LicenseActivationResultTests
{
    [Fact]
    public void Success_Creates_Successful_Result_With_Payload()
    {
        LicensePayload payload = new() { LicenseId = "LIC-001" };

        LicenseActivationResult result = LicenseActivationResult.Success(payload);

        result.IsSuccess.Should().BeTrue();
        result.Error.Should().BeNull();
        result.Payload.Should().BeSameAs(payload);
    }

    [Fact]
    public void Failed_Creates_Failed_Result_With_Error()
    {
        LicenseActivationResult result = LicenseActivationResult.Failed("Network error");

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Network error");
        result.Payload.Should().BeNull();
    }
}
