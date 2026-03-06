namespace Muonroi.Governance.License;

public sealed class LicensePayload
{
    public string? LicenseId { get; set; }
    public string? ProjectId { get; set; }
    public string? TenantId { get; set; }
    public string[]? AllowedFeatures { get; set; }
    public string? Fingerprint { get; set; }
    /// <summary>
    /// The unique hardware identifier this license is bound to.
    /// </summary>
    public string? HardwareId { get; set; }
    /// <summary>
    /// A nonce returned by the central license server during online activation.
    /// Used to salt subsequent action chaining signatures.
    /// </summary>
    public string? ServerNonce { get; set; }
    public DateTimeOffset? NotBefore { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public string? Signature { get; set; }
}
