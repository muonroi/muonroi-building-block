namespace Muonroi.Core.Abstractions.Tests;

public class MTokenPayloadTests
{
    [Fact]
    public void UserGuid_Returns_Value_Or_Empty()
    {
        MTokenPayload payload = new();

        payload.UserGuid.Should().BeEmpty();

        payload.UserGuid = "guid";

        payload.UserGuid.Should().Be("guid");
    }

    [Fact]
    public void Username_Returns_Value_Or_Empty()
    {
        MTokenPayload payload = new();

        payload.Username.Should().BeEmpty();

        payload.Username = "user";

        payload.Username.Should().Be("user");
    }

    [Fact]
    public void TokenValidity_Returns_Value_Or_Empty()
    {
        MTokenPayload payload = new();

        payload.TokenValidity.Should().BeEmpty();

        payload.TokenValidity = "valid";

        payload.TokenValidity.Should().Be("valid");
    }

    [Fact]
    public void TenantId_Returns_Value_Or_Null()
    {
        MTokenPayload payload = new();

        payload.TenantId.Should().BeNull();

        payload.TenantId = "tenant-1";

        payload.TenantId.Should().Be("tenant-1");
    }

    [Fact]
    public void ExtraClaims_Returns_List()
    {
        MTokenPayload payload = new();
        Claim claim = new("role", "admin");

        payload.ExtraClaims.Should().BeEmpty();

        payload.ExtraClaims.Add(claim);

        payload.ExtraClaims.Should().Contain(claim);
    }
}
