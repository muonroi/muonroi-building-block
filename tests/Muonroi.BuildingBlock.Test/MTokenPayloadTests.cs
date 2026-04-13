namespace Muonroi.BuildingBlock.Test;

public class MTokenPayloadTests
{
    [Fact]
    public void UserGuid_Returns_Value_Or_Empty()
    {
        MTokenPayload payload = new();
        Assert.Equal(string.Empty, payload.UserGuid);
        payload.UserGuid = "guid";
        Assert.Equal("guid", payload.UserGuid);
    }

    [Fact]
    public void Username_Returns_Value_Or_Empty()
    {
        MTokenPayload payload = new();
        Assert.Equal(string.Empty, payload.Username);
        payload.Username = "user";
        Assert.Equal("user", payload.Username);
    }

    [Fact]
    public void TokenValidity_Returns_Value_Or_Empty()
    {
        MTokenPayload payload = new();
        Assert.Equal(string.Empty, payload.TokenValidity);
        payload.TokenValidity = "expired";
        Assert.Equal("expired", payload.TokenValidity);
    }

    [Fact]
    public void TenantId_Returns_Value_Or_Null()
    {
        MTokenPayload payload = new();
        Assert.Null(payload.TenantId);
        payload.TenantId = "t1";
        Assert.Equal("t1", payload.TenantId);
    }

    [Fact]
    public void ExtraClaims_Returns_List()
    {
        MTokenPayload payload = new();
        Assert.Empty(payload.ExtraClaims);
        Claim claim = new("role", "admin", ClaimValueTypes.Integer, "iss", "orig");
        payload.ExtraClaims.Add(claim);
        Assert.Contains(claim, payload.ExtraClaims);
    }
}
