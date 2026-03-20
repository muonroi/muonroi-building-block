namespace Muonroi.Governance.License;

/// <summary>
/// Represents the License Payload.
/// </summary>
public sealed class LicensePayload
{
    /// <summary>
    /// Gets or sets the License Id.
    /// </summary>
    public string? LicenseId { get; set; }
    /// <summary>
    /// Gets or sets the Project Id.
    /// </summary>
    public string? ProjectId { get; set; }
    /// <summary>
    /// Gets or sets the Tenant Id.
    /// </summary>
    public string? TenantId { get; set; }
    /// <summary>
    /// Gets or sets the Allowed Features.
    /// </summary>
    public string[]? AllowedFeatures { get; set; }
    /// <summary>
    /// Gets or sets the Fingerprint.
    /// </summary>
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
    /// <summary>
    /// Gets or sets the Not Before.
    /// </summary>
    public DateTimeOffset? NotBefore { get; set; }
    /// <summary>
    /// Gets or sets the Expires At.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; set; }
    /// <summary>
    /// Gets or sets the Signature.
    /// </summary>
    public string? Signature { get; set; }
}
