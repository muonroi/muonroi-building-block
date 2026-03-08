namespace Muonroi.Core.Abstractions.Models;

/// <summary>
/// Represents the payload of a token.
/// </summary>
public class MTokenPayload
{
    /// <summary>
    /// The user GUID.
    /// </summary>
    public string UserGuid { get; set; } = string.Empty;
    /// <summary>
    /// The username.
    /// </summary>
    public string Username { get; set; } = string.Empty;
    /// <summary>
    /// The token validity.
    /// </summary>
    public string TokenValidity { get; set; } = string.Empty;
    /// <summary>
    /// The tenant ID.
    /// </summary>
    public string? TenantId { get; set; }
    /// <summary>
    /// Extra claims.
    /// </summary>
    public List<Claim> ExtraClaims { get; set; } = [];
}
