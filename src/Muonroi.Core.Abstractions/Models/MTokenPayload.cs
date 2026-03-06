namespace Muonroi.Core.Abstractions.Models;

public class MTokenPayload
{
    public string UserGuid { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string TokenValidity { get; set; } = string.Empty;
    public string? TenantId { get; set; }
    public List<Claim> ExtraClaims { get; set; } = [];
}
